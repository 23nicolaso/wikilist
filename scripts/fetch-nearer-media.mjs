import {readFile, writeFile, mkdir, readdir} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {parseHTML} from 'linkedom';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const resources = resolve(root, 'Nearer/Assets/Resources');
const output=process.argv.includes('--stage')?resolve(root,'.nearer-local/media-stage'):resources;
let catalog = JSON.parse(await readFile(resolve(resources, 'nearer-catalog.json'), 'utf8'));
if(process.argv.includes('--culture')){
  const editorial=JSON.parse(await readFile(resolve(root,'scripts/nearer-topics.json'),'utf8'));
  catalog={articles:[...new Set(editorial.collections.flatMap(c=>c.titles))].map(title=>({title}))};
}
const cache = resolve(root, '.nearer-local/media-cache');
await mkdir(cache,{recursive:true});
await mkdir(resolve(output, 'Images'),{recursive:true});
let last = 0;
async function request(url) {
  for(let attempt=0;attempt<6;attempt++) {
    await new Promise(r=>setTimeout(r,Math.max(0,2600-(Date.now()-last)))); last=Date.now();
    try{
      const r=await fetch(url,{headers:{'User-Agent':'Nearer/1.0 (educational Wikipedia game)'},signal:AbortSignal.timeout(20000)});
      if(r.ok)return r;
      const error=Error('Image service returned '+r.status);error.retryAfter=Number(r.headers.get('retry-after'))*1000||0;throw error;
    }catch(error){if(attempt===5)throw error;await new Promise(r=>setTimeout(r,Math.max(error.retryAfter||0,6000*(attempt+1))));}
  }
}
async function wiki(params) {
  const u=new URL('https://en.wikipedia.org/w/api.php');u.search=new URLSearchParams({format:'json',formatversion:'2',...params});
  const filename=resolve(cache,createHash('sha256').update(u.href).digest('hex')+'.json');
  try{return JSON.parse(await readFile(filename,'utf8'));}catch{}
  const d=await(await request(u)).json();if(d.error)throw Error(d.error.info);await writeFile(filename,JSON.stringify(d));return d;
}
const images=[];
for(let offset=0;offset<catalog.articles.length;offset+=40){
  const pages=await wiki({action:'query',titles:catalog.articles.slice(offset,offset+40).map(a=>a.title).join('|'),redirects:'1',prop:'pageimages',piprop:'name',pilicense:'free',pilimit:'50'});
  images.push(...pages.query.pages.filter(p=>p.pageimage));
}
// Prefer a scene from the article to a national flag in the spatial composition.
const scenic = new Set(['Brazil','Japan','South Korea']);
for(const filename of await readdir(resolve(root,'.nearer-local/data-cache'))) {
  if(!filename.endsWith('.json'))continue;
  const data=JSON.parse(await readFile(resolve(root,'.nearer-local/data-cache',filename),'utf8'));
  if(!scenic.has(data.parse?.title))continue;
  const {document}=parseHTML(data.parse.text);
  for(const a of document.querySelectorAll('a.mw-file-description[href]')) {
    const href=a.getAttribute('href');
    if(!href.startsWith('/wiki/File:'))continue;
    const name=decodeURIComponent(href.slice(11)).replaceAll('_',' ');
    if(!/\.(?:jpe?g|png)$/i.test(name)||/flag|coat|map|locator|logo|globe|location|emblem|seal|signature|population|pyramid|chart|blank|symbol/i.test(name))continue;
    const page=images.find(p=>p.title===data.parse.title);
    if(page)page.pageimage=name;
    scenic.delete(data.parse.title);break;
  }
  if(scenic.size===0)break;
}
const normalizeFile = name => name.replace(/^File:/,'').replaceAll('_',' ').normalize('NFC');
const byFile=new Map();
for(let offset=0;offset<images.length;offset+=40){
  const files=await wiki({action:'query',titles:images.slice(offset,offset+40).map(p=>'File:'+p.pageimage).join('|'),prop:'imageinfo',iiprop:'url|extmetadata|mime',iiurlwidth:'320'});
  for(const p of files.query.pages)byFile.set(normalizeFile(p.title),p.imageinfo?.[0]);
}
const plain = value => String(value||'').replace(/<[^>]*>/g,'').replace(/&amp;/g,'&').replace(/&#39;/g,"'").replace(/&quot;/g,'"').trim();
const entries=[];
for(const page of images) {
  const info=byFile.get(normalizeFile(page.pageimage));if(!info?.thumburl)continue;
  const meta=info.extmetadata||{};
  const license=plain(meta.LicenseShortName?.value);
  if(!/^(?:CC BY|CC0|Public domain|OGL 3)/i.test(license))continue;
  const id=createHash('sha256').update(page.pageimage).digest('hex').slice(0,16);
  try {
    let bytes, extension;
    try {bytes=await readFile(resolve(cache,id+'.png'));extension='png';}catch{}
    if(!bytes)try {bytes=await readFile(resolve(cache,id+'.jpg'));extension='jpg';}catch{}
    if(!bytes){
      const r=await request(info.thumburl);const type=r.headers.get('content-type')||'';
      if(!/image\/(?:png|jpeg)/.test(type))continue;
      extension=type.includes('png')?'png':'jpg';bytes=Buffer.from(await r.arrayBuffer());
      await writeFile(resolve(cache,id+'.'+extension),bytes);
    }
    await writeFile(resolve(output,'Images',id+'.'+extension),bytes);
    entries.push({title:page.title,resource:'Images/'+id,author:plain(meta.Artist?.value).slice(0,240),license,licenseUrl:plain(meta.LicenseUrl?.value),url:info.descriptionurl,file:page.pageimage});
    console.log('Image ready:',page.title);
  }catch(e){console.log('Image omitted:',page.title,e.message);}
}
await writeFile(resolve(output,'nearer-media.json'),JSON.stringify({entries},null,2)+'\n');
console.log('Stored',entries.length,'attributed subject images.');
