import {mkdir, readFile, readdir, writeFile, rename} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {parseHTML} from 'linkedom';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const cache=resolve(root,'.nearer-local/data-cache');
const source=JSON.parse(await readFile(resolve(root,'scripts/nearer-topics.json'),'utf8'));
await mkdir(cache,{recursive:true});
let last=0;
async function wiki(params){
  const url=new URL('https://en.wikipedia.org/w/api.php');url.search=new URLSearchParams({format:'json',formatversion:'2',...params});
  const path=resolve(cache,createHash('sha256').update(url.href).digest('hex')+'.json');
  try{return JSON.parse(await readFile(path,'utf8'));}catch{}
  for(let attempt=0;attempt<5;attempt++){
    await new Promise(r=>setTimeout(r,Math.max(0,3200-(Date.now()-last))));last=Date.now();
    try{
      const response=await fetch(url,{headers:{'User-Agent':'Nearer/1.0 (educational Wikipedia connection game)'},signal:AbortSignal.timeout(20000)});
      if(!response.ok){const error=Error('Wikipedia returned '+response.status);error.retryAfter=Number(response.headers.get('retry-after'))*1000||0;throw error;}
      const data=await response.json();if(data.error)throw Error(data.error.info);
      await writeFile(path,JSON.stringify(data));return data;
    }catch(error){if(attempt===4)throw error;console.log('Retrying article service:',error.message);await new Promise(r=>setTimeout(r,Math.max(error.retryAfter||0,6000*(attempt+1))));}
  }
}
function targets(html){
  const {document}=parseHTML(html);
  document.querySelectorAll('table,.navbox,.vertical-navbox,.infobox,.metadata,.reflist,.refbegin,.sidebar,.hatnote,sup,style,script').forEach(e=>e.remove());
  const names=new Set(),redirects=new Set();
  for(const a of document.querySelectorAll('p a[href]')){
    const href=a.getAttribute('href');if(!href.startsWith('/wiki/')||a.classList.contains('new'))continue;
    const title=decodeURIComponent(href.slice(6).split('#')[0]).replaceAll('_',' ');if(title&&!title.includes(':')){names.add(title);if(a.classList.contains('mw-redirect'))redirects.add(title);}
  }
  return {targets:[...names],redirects:[...redirects]};
}
const aliases=new Map(),pages=new Map();
function remember(data){
  for(const item of [...(data.query?.normalized||[]),...(data.query?.redirects||[])])aliases.set(item.from,item.to);
  for(const page of data.query?.pages||[])pages.set(page.title,!page.missing&&page.ns===0?page.title:null);
}
for(const file of await readdir(cache))if(file.endsWith('.json'))try{remember(JSON.parse(await readFile(resolve(cache,file),'utf8')));}catch{}
function canonical(name){const seen=new Set();while(aliases.has(name)&&!seen.has(name)){seen.add(name);name=aliases.get(name);}return pages.has(name)?pages.get(name):undefined;}
const articles=[];
const titles=[...new Set(source.collections.flatMap(c=>c.titles))];
for(const [index,title] of titles.entries()){
  const data=await wiki({action:'parse',page:title,prop:'text|revid',redirects:'1',disableeditsection:'1'});
  if(!data.parse?.text)throw Error('No article: '+title);
  articles.push({requested:title,title:data.parse.title,revision:data.parse.revid,...targets(data.parse.text),collections:[...new Set([...source.collections.filter(c=>c.titles.includes(title)).map(c=>c.id),...(source.affinities?.[title]||[])])]});
  console.log('Read',index+1,'of',titles.length, title);
}
// Existing non-redirect links are identified by the rendered page itself.
// MediaWiki marks redirect links with mw-redirect and missing links with new.
// Resolve the redirect subset instead of re-querying navigation and reference targets.
const redirectNames=new Set(articles.flatMap(a=>a.redirects));
for(const name of new Set(articles.flatMap(a=>a.targets)))if(!redirectNames.has(name)&&canonical(name)===undefined)pages.set(name,name);
const remaining=[...redirectNames].filter(n=>canonical(n)===undefined);
console.log('Resolving',remaining.length,'remaining prose redirects.');
for(let i=0;i<remaining.length;i+=50){
  const batch=remaining.slice(i,i+50);
  remember(await wiki({action:'query',titles:batch.join('|'),redirects:'1',prop:'info'}));
  for(const name of batch)if(canonical(name)===undefined)throw Error('Unresolved redirect: '+name);
  console.log('Reconciled',Math.min(i+50,remaining.length),'of',remaining.length,'redirects');
}
const catalog={schema:2,version:source.version,collections:source.collections.map(c=>c.id),articles:articles.map(a=>{
  const links=[...new Set(a.targets.map(canonical).filter(Boolean))].sort();if(links.length<10)throw Error('Insufficient prose links: '+a.title);
  return {title:a.title,label:source.labels[a.requested]||a.requested,revision:a.revision,url:'https://en.wikipedia.org/w/index.php?'+new URLSearchParams({title:a.title,oldid:String(a.revision)}),collections:a.collections,links};
})};
const path=resolve(root,'Nearer/Assets/Resources/nearer-catalog.json');await writeFile(path+'.pending',JSON.stringify(catalog)+'\n');await rename(path+'.pending',path);
console.log('Wrote',catalog.articles.length,'culture articles in',catalog.collections.length,'editorial collections.');
