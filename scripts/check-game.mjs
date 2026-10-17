import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import worker,{titleFairness,popularityFairness} from '../worker/index.js';
import {DOMParser as HTMLParser} from 'linkedom';
class DOMParser extends HTMLParser {
  parseFromString(...args){const doc=super.parseFromString(...args);for(const cell of doc.querySelectorAll('td,th')){Object.defineProperties(cell,{colSpan:{value:Number(cell.getAttribute('colspan'))||1},rowSpan:{value:Number(cell.getAttribute('rowspan'))||1}})}return doc}
}
const html=await (await worker.fetch(new Request('https://example.test/'))).text();
const elements=new Map();
function element(){return {textContent:'',value:'',style:{},hidden:false,children:[],append(...c){this.children.push(...c)},replaceChildren(...c){this.children=c},setAttribute(){},focus(){},showModal(){},close(){}}}
const context={document:{getElementById(id){if(!elements.has(id))elements.set(id,element());return elements.get(id)},createElement:element,createDocumentFragment:element,addEventListener(){}},Date,Set,Map,Math,Number,String,JSON,console,location:{origin:'https://example.test'},localStorage:{setItem(){},getItem(){return null}},setInterval(){return 1},clearInterval(){},crypto:{randomUUID(){return 'test'}},navigator:{clipboard:{async writeText(){}}}};
vm.createContext(context);
context.URLSearchParams=URLSearchParams;
context.DOMParser=DOMParser;
const script=html.split('<script>')[1].split('</script>')[0].replace(/load\(\);\s*$/,'');
vm.runInContext(script,context);
vm.runInContext(`puzzle={title:'List of scientists',column:'Name',entries:['Albert Einstein','Marie Curie','Pierre Curie','Niels Bohr']};key='test';begin();`,context);
function guess(s){elements.get('guess').value=s;vm.runInContext("$('form').onsubmit({preventDefault(){}})",context)}
guess('EINSTEIN');assert.equal(elements.get('score').textContent,1,'Unique surname accepted');
guess('Albert Einstein');assert.equal(elements.get('score').textContent,1,'Duplicate does not score');
guess('Curie');assert.equal(elements.get('score').textContent,1,'Ambiguous surname rejected');
guess('Márie, Curie!');assert.equal(elements.get('score').textContent,2,'Accents and punctuation ignored');
guess('Not an answer');assert.equal(elements.get('score').textContent,2,'Wrong answer does not score');
vm.runInContext('deadline=Date.now()-100;tick()',context);assert.equal(elements.get('timer').textContent,'0:00');assert.equal(elements.get('form').hidden,true);assert.equal(elements.get('result').hidden,false);assert.equal(elements.get('entries').children[0].children.length,4,'All entries revealed');
guess('Niels Bohr');assert.equal(elements.get('score').textContent,2,'Answers after deadline rejected');
assert.equal((await worker.fetch(new Request('https://example.test/api/article?title=Bad'))).status,400);
assert.equal((await worker.fetch(new Request('https://example.test/missing'))).status,404);
console.log('PASS: matching, duplicates, ambiguous surnames, expiry, reveal, and API guards');
for(const title of ['List of national parks of the United States','List of presidents of France','List of Canadian films','List of Japanese video games','List of rivers in Paris','List of films of 2024','List of military aircraft','List of endemic birds','List of chemical elements'])assert.equal(titleFairness(title).accepted,true,title+' remains eligible');
assert.equal(titleFairness('Albert Einstein').accepted,false);
const daily=n=>Array(30).fill(n);
assert.equal(popularityFairness(daily(50)).accepted,true,'Minimum popularity is inclusive');
assert.equal(popularityFairness(daily(49)).accepted,false,'Below the floor rejected');
assert.equal(popularityFairness(daily(52),Array.from({length:12},()=>daily(0))).accepted,true,'Niche answers need no popularity score');
assert.equal(popularityFairness([100000,...Array(29).fill(5)]).accepted,false,'One-day traffic spike rejected');
assert.equal(popularityFairness([]).accepted,false,'Missing data fails closed');
assert.equal(popularityFairness(Array(13).fill(100)).accepted,false,'Insufficient measured days rejected');
console.log('PASS: niche and country-specific lists eligible, minimum popularity, traffic spikes, missing data');
const originalExtract=vm.runInContext('extract',context);
context.fixture={title:'List of working entries',column:'Name',entries:Array.from({length:12},(_,i)=>'Entry '+i)};
vm.runInContext('extract=()=>fixture',context);
const attempts=[];
context.fetch=async url=>{
  const u=new URL(url,'https://example.test');const title=u.searchParams.get('title');attempts.push(title);
  if(u.pathname==='/api/candidates')return Response.json({titles:['List of temporary failure','List of missing article','List of screening failure','List of working entries']});
  if(title==='List of temporary failure')return new Response('',{status:502});
  if(title==='List of missing article')return new Response('',{status:404});
  if(u.pathname==='/api/screen')return title==='List of screening failure'?new Response('',{status:503}):Response.json({accepted:true});
  return Response.json({html:'fixture',revision:123});
};
await vm.runInContext('load()',context);
assert.equal(vm.runInContext('puzzle.revision',context),123,'Search continues through article and popularity service failures');
assert.equal(elements.get('start').disabled,false);
context.fetch=async url=>url.startsWith('/api/candidates')?Response.json({titles:['List of unavailable']}):new Response('',{status:502});
await vm.runInContext('load()',context);
assert.equal(vm.runInContext('puzzle',context),null,'A failed load cannot start an unchecked puzzle');
assert.equal(elements.get('start').textContent,'Try again');
context.originalExtract=originalExtract;vm.runInContext('extract=originalExtract',context);
const originalFetch=globalThis.fetch;let retryCalls=0;
try{
  globalThis.fetch=async()=>{retryCalls++;return retryCalls===1?new Response('',{status:502}):Response.json({parse:{text:'<table></table>',revid:1}})};
  assert.equal((await worker.fetch(new Request('https://example.test/api/article?title=List%20of%20retry%20fixture'))).status,200);
  assert.equal(retryCalls,2,'Temporary upstream 502 retried');
  globalThis.fetch=async()=>Response.json({error:{code:'missingtitle',info:'Missing article'}});
  assert.equal((await worker.fetch(new Request('https://example.test/api/article?title=List%20of%20missing%20fixture'))).status,404,'Missing article is not a gateway error');
  assert.equal((await worker.fetch(new Request('https://example.test/favicon.ico'))).status,204);
}finally{globalThis.fetch=originalFetch}
console.log('PASS: practice skips 502/404 and screening failures, failed loads stay unplayable, upstream retries, favicon');
if(process.argv.includes('--live')){
  for(const title of ['List of Nobel laureates in Physics','List of national parks of the United States']){
    const response=await worker.fetch(new Request('https://example.test/api/article?title='+encodeURIComponent(title)));
    assert.equal(response.status,200,title+' must load from Wikipedia');
    const article=await response.json();context.articleHTML=article.html;context.articleTitle=title;
    const puzzle=vm.runInContext('extract(articleHTML,articleTitle)',context);
    assert.ok(puzzle?.entries.length>=12,title+' must produce a playable table');
    if(title.includes('Physics'))assert.ok(puzzle.entries.includes('Albert Einstein'),'Must extract real laureates');
    console.log('PASS: live Wikipedia extraction:',title,puzzle.entries.length,'entries, column:',puzzle.column);
  }
  const candidates=await (await worker.fetch(new Request('https://example.test/api/candidates?seed='+new Date().toISOString().slice(0,10)))).json();
  assert.ok(candidates.titles.length>0);
  assert.ok(candidates.titles.every(t=>titleFairness(t).accepted));
  let approved;
  for(const title of candidates.titles){
    const response=await worker.fetch(new Request('https://example.test/api/article?title='+encodeURIComponent(title)));if(!response.ok)continue;
    context.articleHTML=(await response.json()).html;context.articleTitle=title;
    const puzzle=vm.runInContext('extract(articleHTML,articleTitle)',context);if(!puzzle)continue;
    const linked=puzzle.entryTitles;const sample=linked.length<=40?linked:Array.from({length:40},(_,i)=>linked[Math.floor(i*(linked.length-1)/39)]);
    const verdictResponse=await worker.fetch(new Request('https://example.test/api/screen?'+new URLSearchParams({title,entries:JSON.stringify(sample)})));
    assert.equal(verdictResponse.status,200,'Popularity service must work');const verdict=await verdictResponse.json();
    if(verdict.accepted){approved=puzzle;console.log('PASS: complete daily discovery → extraction → fairness:',title,puzzle.entries.length,'answers;',verdict.medianDailyViews,'median daily views');break}
  }
  assert.ok(approved,'Daily selection must produce a playable, screened puzzle');
}
