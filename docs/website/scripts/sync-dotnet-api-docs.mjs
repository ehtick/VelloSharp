import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {dirname, join, relative} from 'node:path';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readdirSync,
  rmSync,
  statSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import {parse as parseYaml} from 'yaml';

const skipSync = process.env.SKIP_DOTNET_API_SYNC === '1';

if (skipSync) {
  console.log('Skipping dotnet API sync (SKIP_DOTNET_API_SYNC=1).');
  process.exit(0);
}

if (!process.env.DOCFX_MSBUILD_ARGS) {
  process.env.DOCFX_MSBUILD_ARGS = '/p:EnableWindowsTargeting=true';
}

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, '..', '..', '..');
const docfxDir = join(repoRoot, 'docs', 'docfx');
const docfxJson = join(docfxDir, 'docfx.json');
const websiteDir = join(repoRoot, 'docs', 'website');
const generatedDir = join(websiteDir, 'generated', 'dotnet-api');
const docfxOutputDir = join(docfxDir, 'obj', 'api');
const apiIndex = join(docfxDir, 'api', 'index.md');
const tocPrimaryPath = join(docfxOutputDir, 'toc.yml');
const tocFallbackPath = join(docfxDir, 'api', 'toc.yml');
const generatedSidebarPath = join(websiteDir, 'sidebars.dotnet.generated.ts');

function run(command, args, options) {
  return new Promise((resolve, reject) => {
    const proc = spawn(command, args, {
      stdio: 'inherit',
      ...options,
    });
    proc.on('error', reject);
    proc.on('close', (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`${command} ${args.join(' ')} exited with code ${code}`));
      }
    });
  });
}

if (!existsSync(docfxJson)) {
  throw new Error(`DocFX configuration not found at ${docfxJson}`);
}

await run('dotnet', ['tool', 'restore'], {cwd: repoRoot});
console.log('Generating DocFX metadata as Markdown...');
await run('dotnet', ['tool', 'run', 'docfx', 'metadata', docfxJson], {cwd: docfxDir});

rmSync(generatedDir, {recursive: true, force: true});
mkdirSync(generatedDir, {recursive: true});

function copyMarkdownFiles(currentDir) {
  for (const entry of readdirSync(currentDir)) {
    const fullPath = join(currentDir, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) {
      copyMarkdownFiles(fullPath);
      continue;
    }

    if (!entry.endsWith('.md')) {
      continue;
    }

    const relativePath = relative(docfxOutputDir, fullPath);
    const destinationPath = join(generatedDir, relativePath);
    mkdirSync(dirname(destinationPath), {recursive: true});
    copyFileSync(fullPath, destinationPath);
    sanitizeMarkdown(destinationPath);
  }
}

copyMarkdownFiles(docfxOutputDir);

if (existsSync(apiIndex)) {
  const destination = join(generatedDir, 'index.md');
  copyFileSync(apiIndex, destination);
  sanitizeMarkdown(destination);
}

console.log('Dotnet API docs synced.');
buildSidebar();

function sanitizeMarkdown(filePath) {
  const original = readFileSync(filePath, 'utf8');
  const unescaped = original.replace(/\\([()<>])/g, '$1');
  let sanitized = escapeGenerics(unescaped);
  sanitized = normalizeHeadings(sanitized);
  if (sanitized !== original) {
    writeFileSync(filePath, sanitized);
  }
}

function escapeGenerics(content) {
  let result = '';
  let depth = 0;
  let insideFence = false;
  let insideInline = false;

  for (let i = 0; i < content.length; i += 1) {
    if (content.startsWith('```', i)) {
      result += '```';
      i += 2;
      insideFence = !insideFence;
      continue;
    }

    const char = content[i];

    if (!insideFence && char === '`') {
      insideInline = !insideInline;
      result += char;
      continue;
    }

    if (!insideFence && !insideInline) {
      if (char === '<' && isGenericStart(content, i)) {
        result += '&lt;';
        depth += 1;
        continue;
      }
      if (char === '>' && depth > 0) {
        result += '&gt;';
        depth -= 1;
        continue;
      }
    }

    result += char;
  }

  return result;
}

function isGenericStart(content, index) {
  const prev = content[index - 1];
  const next = content[index + 1];

  if (!prev || !next) {
    return false;
  }

  if (!/[A-Za-z0-9_\)\]\?]/.test(prev)) {
    return false;
  }

  if (!/[A-Za-z0-9_\[\(]/.test(next)) {
    return false;
  }

  if (prev === '/' || prev === ':') {
    return false;
  }

  return true;
}

function normalizeHeadings(content) {
  const lines = content.split('\n');
  const headingRegex = /^(#{1,6})\s+<a id="([^"]+)"><\/a>\s*(.*)$/;

  for (let i = 0; i < lines.length; i += 1) {
    const match = lines[i].match(headingRegex);
    if (match) {
      const [, hashes, anchorId, rest] = match;
      const label = rest.trim().length > 0 ? rest.trim() : anchorId;
      lines[i] = `${hashes} ${label} {#${anchorId}}`;
    }
  }

  return lines.join('\n');
}

function buildSidebar() {
  const tocSourcePath = [tocPrimaryPath, tocFallbackPath].find((candidate) => existsSync(candidate));
  let sidebarItems = [];
  if (tocSourcePath) {
    const toc = parseYaml(readFileSync(tocSourcePath, 'utf8'));
    const tocItems = Array.isArray(toc) ? toc : toc?.items ?? [];
    sidebarItems = extractNodes(tocItems).filter(Boolean);
  } else {
    console.warn('DocFX toc.yml not found; dotnet sidebar will include only the index page.');
  }

  const sidebarConfig = {
    dotnetApi: [
      ...(existsSync(join(generatedDir, 'index.md')) ? ['index'] : []),
      ...sidebarItems,
    ],
  };

  const moduleSource = `import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = ${JSON.stringify(sidebarConfig, null, 2)};

export default sidebars;
`;

  writeFileSync(generatedSidebarPath, moduleSource);
}

function extractNodes(nodes) {
  const results = [];

  for (const node of nodes) {
    const mapped = mapNode(node);
    if (mapped) {
      results.push(mapped);
    }
  }

  return results;
}

function mapNode(node) {
  const docId = getDocId(node);
  const label = node?.name ?? docId;
  const children = extractNodes(node?.items ?? []);
  const docPath = docId ? getDocFilePath(docId) : null;
  const docExists = !!(docPath && existsSync(docPath));

  if (!docExists && children.length === 0) {
    return null;
  }

  if (children.length === 0) {
    return docId ?? null;
  }

  const category = {
    type: 'category',
    label,
    items: children,
  };

  if (docExists && docId) {
    category.link = {
      type: 'doc',
      id: docId,
    };
  }

  return category;
}

function getDocId(node) {
  if (!node) {
    return null;
  }

  if (typeof node.uid === 'string' && node.uid.length > 0) {
    return node.uid;
  }

  const href = node.href;
  if (typeof href === 'string' && href.length > 0) {
    const [withoutFragment] = href.split('#');
    const normalized = withoutFragment.replace(/\\/g, '/');
    if (normalized.endsWith('.md')) {
      const withoutExtension = normalized.slice(0, -3);
      return withoutExtension.startsWith('./') ? withoutExtension.slice(2) : withoutExtension;
    }
  }

  return null;
}

function getDocFilePath(docId) {
  return join(generatedDir, ...docId.split('/')) + '.md';
}
