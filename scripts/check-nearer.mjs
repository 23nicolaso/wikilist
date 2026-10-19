import assert from 'node:assert/strict';
import {readFile, readdir, stat} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const bank = JSON.parse(await readFile(resolve(root, 'Nearer/Assets/Resources/nearer-puzzles.json'), 'utf8'));
const resources = resolve(root, 'Nearer/Assets/Resources');
const catalog = JSON.parse(await readFile(resolve(resources, 'nearer-catalog.json'), 'utf8'));
const media = JSON.parse(await readFile(resolve(resources, 'nearer-media.json'), 'utf8'));
const articleTitles = new Set(catalog.articles.map(article => article.title));
assert.equal(articleTitles.size, catalog.articles.length);
assert.equal(catalog.schema, 2);
assert.ok(catalog.articles.length >= 100);
assert.equal(catalog.collections.length, 6);
for(const title of ['Starbucks',"McDonald's",'Taylor Swift','Christopher Nolan','Minecraft','Lionel Messi'])assert.ok(articleTitles.has(title));
for (const article of catalog.articles) {
  assert.ok(article.revision > 0 && article.links.length > 0);
  assert.equal(new Set(article.links).size, article.links.length);
  assert.ok(article.collections.length>0 && article.collections.every(group=>catalog.collections.includes(group)));
  assert.ok(!/[\u2014\\/]/u.test(article.label));
  const source = new URL(article.url);
  assert.equal(source.origin, 'https://en.wikipedia.org');
  assert.equal(source.searchParams.get('oldid'), String(article.revision));
}
const imageFiles = await readdir(resolve(resources, 'Images'));
for (const credit of media.entries) {
  assert.ok(articleTitles.has(credit.title));
  assert.ok(credit.author && credit.license);
  assert.equal(new URL(credit.url).origin, 'https://commons.wikimedia.org');
  const filename = imageFiles.find(file => credit.resource === 'Images/' + file.replace(/\.(?:png|jpg)$/i, '') && /\.(?:png|jpg)$/i.test(file));
  assert.ok(filename, 'Missing subject image: ' + credit.title);
  assert.ok((await stat(resolve(resources, 'Images', filename))).size > 0);
}
assert.equal(bank.schema, 1);
assert.ok(bank.puzzles.length >= 7);
assert.equal(new Set(bank.puzzles.map(p => p.id)).size, bank.puzzles.length);
for (const p of bank.puzzles) {
  assert.equal(p.topics.length, 5);
  assert.ok(p.anchorLinkCount > 0 && p.anchorRevision > 0);
  let comparisons = 0;
  for (let i=0;i<5;i++) {
    const t = p.topics[i];
    assert.ok(t.evidence.length > 0 && t.sharedCount >= t.evidence.length);
    assert.equal(t.unionCount, p.anchorLinkCount + t.linkCount - t.sharedCount);
    assert.ok(Math.abs(t.similarity - t.sharedCount / t.unionCount) < 1e-8);
    const url = new URL(t.url);
    assert.equal(url.origin, 'https://en.wikipedia.org');
    assert.equal(url.searchParams.get('oldid'), String(t.revision));
    assert.equal(url.searchParams.get('title'), t.title);
    assert.ok(!/[\u2014\\/]/u.test(t.label));
    for(let j=i+1;j<5;j++) if(Math.abs(t.similarity - p.topics[j].similarity) > p.tieTolerance) comparisons++;
  }
  assert.equal(comparisons, p.informativePairs);
  assert.ok(comparisons >= 6);
}
console.log(`PASS: seven reference boards, ${catalog.articles.length} source articles, ${media.entries.length} credited images, revision links, overlap arithmetic, tie separation, and labels`);
