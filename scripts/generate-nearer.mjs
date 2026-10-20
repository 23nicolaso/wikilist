import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {parseHTML} from 'linkedom';
import {createHash} from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const cache = resolve(root, '.nearer-local', 'data-cache');
await mkdir(cache, {recursive: true});
const boards = [
  ['coffee', 'Coffee', 'Science and nature', ['Espresso', 'Caffeine', 'Tea', 'Chocolate', 'Wine']],
  ['ocean', 'Ocean', 'Science and nature', ['Sea', 'Coral reef', 'Whale', 'River', 'Desert']],
  ['jazz', 'Jazz', 'Culture', ['Blues', 'Saxophone', 'Rock music', 'Hip hop music', 'Ballet']],
  ['japan', 'Japan', 'Places and history', ['Tokyo', 'Samurai', 'Anime', 'South Korea', 'Brazil']],
  ['volcano', 'Volcano', 'Science and nature', ['Lava', 'Earthquake', 'Mountain', 'Geyser', 'Glacier']],
  ['cinema', 'Film', 'Culture', ['Animation', 'Cinematography', 'Television', 'Photography', 'Theatre']],
  ['bicycle', 'Bicycle', 'Places and history', ['Cycling', 'Mountain bike', 'Motorcycle', 'Automobile', 'Train']],
];
let lastRequest = 0;
async function wiki(params) {
  const url = new URL('https://en.wikipedia.org/w/api.php');
  url.search = new URLSearchParams({format: 'json', formatversion: '2', ...params});
  const filename = resolve(cache, createHash('sha256').update(url.href).digest('hex') + '.json');
  try { return JSON.parse(await readFile(filename, 'utf8')); } catch {}
  const legacyName = Buffer.from(url.search).toString('base64url') + '.json';
  if (legacyName.length < 240) {
    try {
      const previous = JSON.parse(await readFile(resolve(cache, legacyName), 'utf8'));
      await writeFile(filename, JSON.stringify(previous));
      return previous;
    } catch {}
  }
  for (let attempt = 0; attempt < 5; attempt++) {
    await new Promise(r => setTimeout(r, Math.max(0, 2600 - (Date.now() - lastRequest))));
    lastRequest = Date.now();
    try {
      const response = await fetch(url, {headers: {'User-Agent': 'Nearer/1.0 (educational daily Wikipedia game)'}, signal: AbortSignal.timeout(15000)});
      if (!response.ok) {
        const error = Error('Wikipedia returned ' + response.status);
        error.retryAfter = Number(response.headers.get('retry-after')) * 1000 || 0;
        throw error;
      }
      const data = await response.json();
      if (data.error) throw Error(data.error.info);
      await writeFile(filename, JSON.stringify(data));
      return data;
    } catch (error) {
      if (attempt === 4) throw error;
      console.log('Waiting for Wikipedia before retrying');
      await new Promise(r => setTimeout(r, Math.min(30000, Math.max(error.retryAfter || 0, 4000 * (attempt + 1)))));
    }
  }
}
function proseTargets(html) {
  const {document} = parseHTML(html);
  document.querySelectorAll('table,.navbox,.vertical-navbox,.infobox,.metadata,.reflist,.refbegin,.sidebar,.hatnote,sup,style,script').forEach(e => e.remove());
  const links = new Set();
  for (const a of document.querySelectorAll('p a[href]')) {
    const href = a.getAttribute('href');
    if (!href.startsWith('/wiki/') || a.classList.contains('new')) continue;
    const title = decodeURIComponent(href.slice(6).split('#')[0]).replaceAll('_', ' ');
    if (title && !title.includes(':')) links.add(title);
  }
  return [...links];
}
const articles = new Map();
for (const title of new Set(boards.flatMap(b => [b[1], ...b[3]]))) {
  const data = await wiki({action: 'parse', page: title, prop: 'text|revid', redirects: '1', disableeditsection: '1'});
  if (!data.parse?.text) throw Error('No article for ' + title);
  articles.set(title, {title: data.parse.title, revision: data.parse.revid, targets: proseTargets(data.parse.text)});
  console.log('Read', title);
}

// Resolve every target, including redirects and missing pages, before set comparisons.
const names = [...new Set([...articles.values()].flatMap(a => a.targets))];
const canonical = new Map();
const aliases = new Map();
const pageRecords = new Map();
let continuation = {};
let batches = 0;
do {
  const data = await wiki({action: 'query', generator: 'links', titles: [...articles.values()].map(a => a.title).join('|'), gplnamespace: '0', gpllimit: '500', redirects: '1', prop: 'info', ...continuation});
  for (const item of [...(data.query.normalized || []), ...(data.query.redirects || [])]) aliases.set(item.from, item.to);
  for (const page of data.query.pages) pageRecords.set(page.title, !page.missing && page.ns === 0 ? page.title : null);
  continuation = data.continue;
  batches++;
  let remaining = 0;
  for (const name of names) {
    let next = name;
    const seen = new Set();
    while (aliases.has(next) && !seen.has(next)) {seen.add(next); next = aliases.get(next);}
    if (!pageRecords.has(next)) remaining++;
  }
  console.log('Bulk redirect batch', batches, 'with', names.length - remaining, 'of', names.length, 'prose targets resolved');
  if (remaining <= 400) break;
} while (continuation);
for (const name of names) {
  let next = name;
  const seen = new Set();
  while (aliases.has(next) && !seen.has(next)) {seen.add(next); next = aliases.get(next);}
  if (pageRecords.has(next)) canonical.set(name, pageRecords.get(next));
}
// Saved revisions can mention targets absent from the current article graph.
const unresolved = names.filter(name => !canonical.has(name));
for (let i = 0; i < unresolved.length; i += 50) {
  const batch = unresolved.slice(i, i + 50);
  const data = await wiki({action: 'query', titles: batch.join('|'), redirects: '1', prop: 'info'});
  const mapping = new Map([...(data.query.normalized || []), ...(data.query.redirects || [])].map(x => [x.from, x.to]));
  const pages = new Map(data.query.pages.map(p => [p.title, p]));
  for (const name of batch) {
    let next = name;
    const seen = new Set();
    while (mapping.has(next) && !seen.has(next)) {seen.add(next); next = mapping.get(next);}
    const page = pages.get(next);
    canonical.set(name, page && !page.missing && page.ns === 0 ? page.title : null);
  }
  console.log('Resolved remaining targets', Math.min(i + 50, unresolved.length), 'of', unresolved.length);
}
for (const article of articles.values()) article.links = new Set(article.targets.map(x => canonical.get(x)).filter(Boolean));
const titleLabels = {'Film': 'Cinema', 'Hip hop music': 'Hip hop', 'Rock music': 'Rock', 'Mountain bike': 'Mountain bike', 'Automobile': 'Car', 'Coral reef': 'Coral reef'};
const generic = new Set(['US$', 'United States', 'United Kingdom', 'France', 'Germany', 'Italy', 'China', 'India', 'Europe', 'English language', 'Latin', 'World War II', 'World War I', 'Ancient Greece', 'ISBN', 'International Standard Book Number']);
const sourceUrl = a => 'https://en.wikipedia.org/w/index.php?' + new URLSearchParams({title: a.title, oldid: String(a.revision)});
const puzzles = boards.map(([id, title, category, topics], day) => {
  const anchor = articles.get(title);
  const entries = topics.map(name => {
    const article = articles.get(name);
    const shared = [...anchor.links].filter(x => article.links.has(x));
    const union = anchor.links.size + article.links.size - shared.length;
    const useful = shared.filter(x => !generic.has(x) && !/^\d|\$/.test(x));
    const evidence = [...useful, ...shared.filter(x => !useful.includes(x))].slice(0, 8);
    return {title: article.title, label: titleLabels[name] || name, revision: article.revision, url: sourceUrl(article), linkCount: article.links.size, sharedCount: shared.length, unionCount: union, similarity: shared.length / union, evidence};
  }).sort((a,b) => b.similarity - a.similarity);
  if (entries.some(e => e.evidence.length === 0)) throw Error('Missing evidence for ' + id);
  const max = entries[0].similarity;
  const tieTolerance = max * 0.08;
  const informativePairs = entries.flatMap((a,i) => entries.slice(i+1).map(b => Math.abs(a.similarity-b.similarity) > tieTolerance)).filter(Boolean).length;
  if (informativePairs < 6) throw Error('Insufficient scoring separation for ' + id);
  return {id, day, category, anchor: titleLabels[title] || title, anchorTitle: anchor.title, anchorRevision: anchor.revision, anchorUrl: sourceUrl(anchor), anchorLinkCount: anchor.links.size, tieTolerance, informativePairs, topics: entries};
});
const output = {schema: 1, epoch: '2026-10-18', extraction: 'Unique article-prose links. Tables, navigation, references and missing pages excluded. Redirects reconciled. Jaccard overlap. Ties within eight percent of the board maximum.', license: 'CC BY-SA 4.0', licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/', puzzles};
const target = resolve(root, 'Nearer', 'Assets', 'Resources', 'nearer-puzzles.json');
await mkdir(dirname(target), {recursive: true});
await writeFile(target, JSON.stringify(output, null, 2) + '\n');
for (const p of puzzles) console.log(p.anchor + ': ' + p.topics.map(t => t.label + ' ' + t.similarity.toFixed(4)).join(', ') + '; ' + p.informativePairs + ' scoring pairs');
console.log('Wrote seven frozen boards.');
