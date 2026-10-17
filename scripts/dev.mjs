import {createServer} from 'node:http';
import worker from '../worker/index.js';
const port=Number(process.env.PORT)||3000;
createServer(async(req,res)=>{try{const response=await worker.fetch(new Request('http://localhost:'+port+req.url));res.writeHead(response.status,Object.fromEntries(response.headers));res.end(Buffer.from(await response.arrayBuffer()))}catch{res.writeHead(500);res.end('Unexpected server error')}}).listen(port,'127.0.0.1',()=>console.log('Listwise is ready at http://localhost:'+port));
