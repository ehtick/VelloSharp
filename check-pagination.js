const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

function getMime(ext) {
  switch (ext) {
    case '.html': return 'text/html';
    case '.css': return 'text/css';
    case '.js': return 'application/javascript';
    case '.json': return 'application/json';
    case '.svg': return 'image/svg+xml';
    case '.png': return 'image/png';
    case '.jpg':
    case '.jpeg': return 'image/jpeg';
    case '.woff': return 'font/woff';
    case '.woff2': return 'font/woff2';
    case '.ttf': return 'font/ttf';
    case '.eot': return 'application/vnd.ms-fontobject';
    case '.map': return 'application/json';
    default: return 'text/plain';
  }
}

async function run() {
  const rootDir = path.resolve('docs/docfx/_site');
  const server = http.createServer((req, res) => {
    const urlPath = decodeURIComponent(req.url.split('?')[0]);
    let filePath = path.join(rootDir, urlPath);
    if (urlPath.endsWith('/')) {
      filePath = path.join(rootDir, urlPath, 'index.html');
    }
    if (!path.extname(filePath)) {
      filePath += '.html';
    }
    fs.readFile(filePath, (err, data) => {
      if (err) {
        res.writeHead(404);
        res.end('Not found');
      } else {
        res.writeHead(200, { 'Content-Type': getMime(path.extname(filePath)) });
        res.end(data);
      }
    });
  });

  await new Promise(resolve => server.listen(0, resolve));
  const port = server.address().port;
  const baseUrl = `http://127.0.0.1:${port}`;

  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto(`${baseUrl}/index.html`, { waitUntil: 'load' });
  await page.waitForSelector('#search', { state: 'visible', timeout: 15000 });
  await page.fill('#search-query', 'renderer');
  await page.waitForSelector('#search-results', { state: 'visible', timeout: 10000 });
  await page.waitForFunction(() => document.querySelectorAll('#pagination li').length > 0, null, { timeout: 10000 });
  const rect = await page.$eval('#pagination', el => {
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height };
  });
  const center = { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
  const elementAtCenter = await page.evaluate(({x, y}) => {
    const el = document.elementFromPoint(x, y);
    if (!el) return null;
    const describe = node => node.tagName + (node.id ? '#' + node.id : '') + (node.className ? '.' + node.className.split(' ').join('.') : '');
    const chain = [];
    let current = el;
    while (current) {
      chain.push(describe(current));
      if (current === document.body) break;
      current = current.parentElement;
    }
    return { chain, top: describe(el) };
  }, center);
  console.log('Pagination rect:', rect);
  console.log('Element at center:', elementAtCenter);
  await browser.close();
  server.close();
}

run().catch(err => {
  console.error(err);
  process.exitCode = 1;
});
