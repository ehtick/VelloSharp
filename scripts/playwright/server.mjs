#!/usr/bin/env node
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { stat } from 'fs/promises';
import { join, normalize, extname } from 'path';

const [,, rootArg, portArg] = process.argv;

if (!rootArg || !portArg) {
  console.error('Usage: node server.mjs <root> <port>');
  process.exit(1);
}

const root = normalize(rootArg);
const port = Number(portArg);

const contentTypes = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.js', 'application/javascript; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.wasm', 'application/wasm'],
  ['.json', 'application/json; charset=utf-8'],
  ['.ico', 'image/x-icon'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml'],
]);

const server = createServer(async (req, res) => {
  try {
    const url = new URL(req.url ?? '/', `http://localhost:${port}`);
    let relativePath = url.pathname;
    if (relativePath.endsWith('/')) {
      relativePath = `${relativePath}index.html`;
    }

    const filePath = normalize(join(root, relativePath));
    if (!filePath.startsWith(root)) {
      res.writeHead(403);
      res.end('Forbidden');
      return;
    }

    const fileStat = await stat(filePath);
    if (fileStat.isDirectory()) {
      res.writeHead(403);
      res.end('Forbidden');
      return;
    }

    const data = await readFile(filePath);
    const contentType = contentTypes.get(extname(filePath)) ?? 'application/octet-stream';
    res.writeHead(200, { 'content-type': contentType });
    res.end(data);
  } catch (err) {
    if (err && err.code === 'ENOENT') {
      res.writeHead(404);
      res.end('Not found');
    } else {
      res.writeHead(500);
      res.end(String(err));
    }
  }
});

server.listen(port, () => {
  console.log('READY');
});

const shutdown = () => {
  server.close(() => process.exit(0));
};

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
