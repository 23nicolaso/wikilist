import {createServer} from 'node:http';
import {readFile, stat} from 'node:fs/promises';
import {resolve, dirname, sep, extname} from 'node:path';
import {fileURLToPath} from 'node:url';

const build = resolve(dirname(fileURLToPath(import.meta.url)), '../Nearer/Builds/Web');
const port = Number(process.env.PORT || 4173);
const mime = {'.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json', '.png': 'image/png', '.ico': 'image/x-icon'};
createServer(async (request, response) => {
  try {
    const url = new URL(request.url, 'http://localhost');
    const pathname = decodeURIComponent(url.pathname);
    const file = resolve(build, '.' + (pathname === '/' ? '/index.html' : pathname));
    if (!file.startsWith(build + sep)) {response.writeHead(403); response.end(); return;}
    if (url.pathname === '/favicon.ico') {response.writeHead(204); response.end(); return;}
    const info = await stat(file);
    if (!info.isFile()) throw Error('Not a file');
    response.writeHead(200, {'Content-Type': mime[extname(file)] || 'application/octet-stream', 'Content-Length': info.size, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff'});
    response.end(request.method === 'HEAD' ? undefined : await readFile(file));
  } catch {
    response.writeHead(404, {'Content-Type': 'text/plain; charset=utf-8'});
    response.end('Build not found. Build the browser game from the Nearer menu in Unity.');
  }
}).listen(port, '127.0.0.1', () => console.log('Nearer is ready at http://localhost:' + port));
