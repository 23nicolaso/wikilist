(function () {
  'use strict';
  const $ = id => document.getElementById(id);
  let engine = null, model = null, puzzleId = '', rows = [], anchorPortrait = null;
  let media = new Map(), pathSelection = null, pathsPuzzle = '', previousFocus = null;
  const statsKey = 'neardle.stats.v1';
  let rounds = [], persistentStats = true, resultFocus = null;
  const popupRounds = new Set();
  try {
    let saved = [];
    const raw = window.localStorage.getItem(statsKey);
    try { saved = JSON.parse(raw || '[]'); } catch (_) { /* Start a clean record if saved data is damaged. */ }
    if (Array.isArray(saved)) {
      const seen = new Set();
      rounds = saved.filter(round => round && typeof round.id === 'string' && round.id.length <= 200 && Number.isFinite(round.score) && round.score >= 0 && round.score <= 100 && !seen.has(round.id) && seen.add(round.id));
    }
  } catch (_) { persistentStats = false; }
  function recordRound(state) {
    if (rounds.some(round => round.id === state.puzzleId)) return;
    rounds.push({ id: state.puzzleId, score: state.score });
    try { window.localStorage.setItem(statsKey, JSON.stringify(rounds)); }
    catch (_) { persistentStats = false; }
  }
  function closeResults() { if ($('results').open || $('results').hasAttribute('open')) $('results').close(); }
  function showResults(completed = false) {
    const best = rounds.length ? rounds.reduce((maximum, round) => Math.max(maximum, round.score), 0) : 0;
    const average = rounds.length ? rounds.reduce((sum, round) => sum + round.score, 0) / rounds.length : 0;
    const score = completed ? model.score : (rounds.at(-1)?.score || 0);
    text($('results-title'), completed ? 'Round complete' : 'Your scores');
    text($('score-caption'), completed ? 'This round' : 'Latest round');
    text($('summary-score'), String(score)); text($('best-score'), String(best)); text($('average-score'), average.toFixed(1));
    $('score-ring').style.setProperty('--score', String(score) + '%');
    $('score-ring').setAttribute('aria-label', (completed ? 'This round: ' : 'Latest round: ') + score + ' out of 100');
    text($('rounds-played'), rounds.length + (rounds.length === 1 ? ' round played' : ' rounds played'));
    text($('stats-storage'), persistentStats ? 'Saved on this browser.' : 'Stats last for this session.');
    $('score-bars').replaceChildren();
    rounds.slice(-8).forEach((round, slot) => {
      const column = element('div', 'score-column');
      const value = element('span', 'bar-value', String(round.score));
      const track = element('div', 'bar-track'), bar = element('div', 'score-bar'); bar.style.height = round.score + '%';
      track.append(bar); column.append(value, track, element('span', 'bar-label', String(Math.max(0, rounds.length - 8) + slot + 1)));
      column.setAttribute('aria-label', 'Round ' + (Math.max(0, rounds.length - 8) + slot + 1) + ': ' + round.score + ' points');
      $('score-bars').append(column);
    });
    if (!rounds.length) $('score-bars').append(element('p', 'stats-empty', 'Finish a round to start your record.'));
    $('next-result').hidden = !model?.revealed || model.animating;
    text($('review-results'), completed ? 'Review answers' : 'Back to game');
    resultFocus = document.activeElement;
    if ($('details').hasAttribute('open')) $('details').close();
    if (!$('results').hasAttribute('open')) $('results').showModal();
  }
  const element = (tag, className, text) => {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  };
  const text = (node, value) => { if (node.textContent !== value) node.textContent = value; };
  const fraction = value => Math.min(1, Math.max(0, Number(value) || 0));
  const percent = value => (fraction(value) * 100).toFixed(3) + '%';
  const send = (action, extra = {}) => {
    if (engine) engine.SendMessage('Nearer', 'BrowserInput', JSON.stringify({ action, ...extra }));
  };
  function link(title, url) {
    const node = element('a', '', title);
    // Both game data and media credits are restricted to attributed public sources.
    if (/^https:\/\/(en\.wikipedia\.org|commons\.wikimedia\.org|creativecommons\.org)\//.test(url || '')) node.href = url;
    node.target = '_blank'; node.rel = 'noopener'; return node;
  }
  function portrait(parent, title) {
    const wrapper = element('div', 'portrait');
    const image = element('img'); image.alt = ''; image.hidden = true; image.draggable = false;
    const initial = element('span', 'initial', title.substring(0, 1).toUpperCase());
    image.addEventListener('error', () => { image.hidden = true; initial.hidden = false; });
    wrapper.append(image, initial); parent.append(wrapper);
    const view = { wrapper, image, initial, title };
    updatePortrait(view); return view;
  }
  function updatePortrait(view) {
    const source = media.get(view.title)?.browserImage;
    if (source && /^Images\/[a-zA-Z0-9_-]+\.(png|jpg|jpeg)$/i.test(source)) {
      if (view.image.getAttribute('src') !== source) view.image.src = source;
      view.image.hidden = false; view.initial.hidden = true;
    }
  }
  function createLanes(state) {
    $('lanes').replaceChildren(); $('anchor-portrait').replaceChildren();
    anchorPortrait = portrait($('anchor-portrait'), state.anchorTitle);
    const place = (index, distance) => {
      if (!model || model.revealed || model.animating || !engine) return;
      distance = fraction(distance); rows[index].card.style.left = percent(distance); send('move', { index, distance });
    };
    rows = new Array(5);
    (state.order || [0, 1, 2, 3, 4]).forEach((index, slot) => {
      const topic = state.topics[index];
      const lane = element('div', 'topic-slot'); lane.dataset.index = index;
      const axis = element('div', 'person-axis'); lane.append(axis); $('lanes').append(lane);
      const numberLine = element('div', 'number-line'); numberLine.setAttribute('aria-hidden', 'true'); axis.append(numberLine);
      for (let tick = 0; tick <= 50; tick++) {
        const mark = element('span', 'axis-tick' + (tick % 5 === 0 ? ' major-tick' : ''));
        mark.style.left = percent(tick / 50); mark.setAttribute('aria-hidden', 'true'); axis.append(mark);
        if (slot === 4 && tick % 5 === 0) { const label = element('span', 'tick-label', String(tick * 2)); label.style.left = percent(tick / 50); label.setAttribute('aria-hidden', 'true'); axis.append(label); }
      }
      const connectors = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); connectors.classList.add('correction-connectors');
      connectors.setAttribute('viewBox', '0 0 1000 100'); connectors.setAttribute('preserveAspectRatio', 'none'); connectors.setAttribute('aria-hidden', 'true'); axis.append(connectors);
      const atPointer = event => { const bounds = axis.getBoundingClientRect(); return bounds.width > 0 ? fraction((event.clientX - bounds.left) / bounds.width) : null; };
      axis.addEventListener('click', event => {
        if (event.target.closest?.('.topic')) return;
        const value = atPointer(event); if (value !== null) place(index, value);
      });
      const original = element('span', 'target-circle'); original.hidden = true; original.setAttribute('aria-hidden', 'true');
      const correction = document.createElementNS('http://www.w3.org/2000/svg', 'path'); correction.classList.add('correction-line'); correction.setAttribute('vector-effect', 'non-scaling-stroke'); correction.style.display = 'none'; connectors.append(correction);
      const card = element('div', 'topic');
      card.tabIndex = 0; card.setAttribute('role', 'slider'); card.setAttribute('aria-orientation', 'horizontal'); card.setAttribute('aria-valuemin', '0'); card.setAttribute('aria-valuemax', '100');
      card.setAttribute('aria-label', 'Distance of ' + topic.label + ' from ' + state.anchor); card.title = topic.label; card.style.setProperty('--label-row', '0px');
      const badge = element('span', 'point-award'); badge.hidden = true;
      card.append(badge);
      const photo = portrait(card, topic.title);
      const marker = element('span', 'topic-marker'); marker.setAttribute('aria-hidden', 'true'); card.append(marker);
      const label = element('div', 'topic-label', topic.label); label.title = topic.label; lane.prepend(label);
      let pointer = null;
      card.addEventListener('pointerdown', event => {
        event.stopPropagation(); if (event.button !== undefined && event.button !== 0) return;
        send('select', { index }); card.focus();
        if (!model || model.revealed || model.animating || !engine) return;
        event.preventDefault(); pointer = event.pointerId; card.setPointerCapture?.(pointer);
      });
      card.addEventListener('pointermove', event => { if (pointer === null || event.pointerId !== pointer) return; const value = atPointer(event); if (value !== null) place(index, value); });
      card.addEventListener('pointerup', event => { if (event.pointerId !== pointer) return; pointer = null; if (card.hasPointerCapture?.(event.pointerId)) card.releasePointerCapture(event.pointerId); });
      card.addEventListener('pointercancel', () => { pointer = null; });
      card.addEventListener('lostpointercapture', () => { pointer = null; });
      card.addEventListener('focus', () => send('select', { index }));
      card.addEventListener('keydown', event => {
        if (!model || model.revealed || model.animating) return;
        const step = event.shiftKey ? 0.05 : 0.01, current = Number(card.getAttribute('aria-valuenow')) / 100;
        let value;
        if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') value = current - step;
        else if (event.key === 'ArrowRight' || event.key === 'ArrowUp') value = current + step;
        else if (event.key === 'Home') value = 0;
        else if (event.key === 'End') value = 1;
        else return;
        event.preventDefault(); event.stopPropagation(); place(index, value);
      });
      axis.append(original, card);
      rows[index] = { lane, card, badge, photo, original, correction, slot };
    });
    pathsPuzzle = ''; pathSelection = null;
  }
  function renderPaths(state) {
    const panel = $('paths-panel'); panel.hidden = !state.revealed || state.animating;
    if (panel.hidden || (pathsPuzzle === state.puzzleId && pathSelection === state.selected)) return;
    $('path-rows').replaceChildren();
    (state.order || [0, 1, 2, 3, 4]).forEach(index => {
      const topic = state.topics[index];
      const row = element('div', 'path-row' + (state.selected === index ? ' selected' : ''));
      const name = element('button', 'path-name', topic.label + '   +' + topic.points + ' points');
      name.addEventListener('click', () => send('select', { index })); row.append(name);
      row.append(element('p', 'path-count', topic.sharedCount + ' shared links'));
      const examples = element('div', 'shared-examples');
      const sample = (topic.evidence || []).slice(0, 3);
      examples.title = sample.join(' \u00b7 ');
      sample.forEach((title, step) => {
        if (step) examples.append(document.createTextNode(' \u00b7 '));
        examples.append(link(title, 'https://en.wikipedia.org/wiki/' + encodeURIComponent(title.replace(/ /g, '_'))));
      });
      row.append(examples);
      $('path-rows').append(row);
    });
    pathsPuzzle = state.puzzleId; pathSelection = state.selected;
  }
  function receive(state) {
    if (!state || !Array.isArray(state.topics) || state.topics.length !== 5) return;
    const previous = model; model = state;
    if (puzzleId !== state.puzzleId) {
      closeResults();
      document.body.classList.remove('correcting');
      createLanes(state); puzzleId = state.puzzleId;
    }
    for (let p = 0; p < 5; p++) document.body.classList.toggle('palette-' + p, p === state.question % 5);
    document.body.classList.toggle('images-off', !state.images);
    document.body.classList.toggle('motion-reduced', state.reducedMotion);
    document.body.classList.toggle('correcting', state.correcting);
    text($('map-number'), 'ROUND ' + String(state.question + 1).padStart(2, '0'));
    text($('prompt'), 'How close to ' + state.anchor + '?'); text($('anchor-name'), state.anchor);
    text($('images-toggle'), state.images ? 'Images on' : 'Images off'); $('images-toggle').disabled = !engine;
    $('sources-button').disabled = !engine;
    $('result-score').hidden = false; $('result-score').style.visibility = state.revealed ? 'visible' : 'hidden';
    $('result-score').setAttribute('aria-hidden', String(!state.revealed));
    text($('result-score'), state.score + ' / 100 points' + (state.animating ? '' : ' · ' + state.correct + ' of ' + state.total + ' comparisons'));
    rows.forEach((row, index) => {
      const topic = state.topics[index];
      row.card.style.left = percent(topic.distance);
      row.card.setAttribute('aria-valuenow', String(Math.round(fraction(topic.distance) * 100)));
      row.card.setAttribute('aria-valuetext', Math.round(fraction(topic.distance) * 100) + ' out of 100' + (state.revealed && topic.awarded ? '; target ' + Math.round(fraction(topic.reference) * 100) : ''));
      row.card.setAttribute('aria-readonly', String(state.revealed)); row.card.setAttribute('aria-disabled', String(!engine || state.animating));
      row.card.style.zIndex = state.selected === index ? '10' : '3';
      row.lane.classList.toggle('selected', state.selected === index);
      text(row.badge, '+' + topic.points + ' points'); row.badge.hidden = !state.revealed || !topic.awarded;
      row.original.style.left = percent(topic.reference); row.original.hidden = !state.revealed || !topic.awarded;
      row.original.title = topic.label + ': target ' + Math.round(topic.reference * 100);
      const from = fraction(topic.distance) * 1000, to = fraction(topic.reference) * 1000;
      row.correction.setAttribute('d', 'M ' + from + ' 50 L ' + to + ' 50');
      row.correction.style.display = state.revealed && topic.awarded && Math.abs(topic.distance - topic.reference) > 0.001 ? '' : 'none';
      const opacity = '1';
      row.original.style.opacity = opacity; row.correction.style.opacity = opacity;
    });
    text($('main-action'), state.animating ? (state.correcting ? 'Correcting…' : 'Counting points…') : state.revealed ? 'Next' : 'Reveal');
    $('main-action').disabled = !engine || state.animating;
    $('share-action').hidden = !state.revealed || state.animating;
    $('paths-button').hidden = !state.revealed || state.animating;
    text($('round-status'), state.notice || (state.animating ? 'Revealing points and targets, top to bottom.' : state.revealed ? 'Your photos stay put. Green circles mark the targets; dotted lines connect them to your guesses.' : 'Place each topic on its own ruler. Drag a photo, or select one and use the arrow keys.'));
    renderPaths(state);
    if (state.revealed && !state.animating && state.topics.every(topic => topic.awarded) && Number.isFinite(state.score) && state.score >= 0 && state.score <= 100) {
      recordRound(state);
      if (!popupRounds.has(state.puzzleId)) {
        popupRounds.add(state.puzzleId);
        if (previous?.puzzleId === state.puzzleId && previous.animating) showResults(true);
      }
    }
  }
  function showDetails(title) {
    previousFocus = document.activeElement; const body = $('dialog-body'); body.replaceChildren(element('h2', '', title));
    $('details').showModal(); return body;
  }
  $('stats-button').addEventListener('click', () => showResults(false));
  $('close-results').addEventListener('click', closeResults);
  $('review-results').addEventListener('click', closeResults);
  $('next-result').addEventListener('click', () => { closeResults(); if (model?.revealed && !model.animating) send('next'); });
  $('results').addEventListener('close', () => resultFocus?.focus());
  $('main-action').addEventListener('click', () => { if (model && !model.animating) send(model.revealed ? 'next' : 'reveal'); });
  $('images-toggle').addEventListener('click', () => send('images'));
  $('share-action').addEventListener('click', () => send('share'));
  $('help-button').addEventListener('click', () => {
    const body = showDetails('A sense of connection');
    body.append(element('p', '', 'Place each topic on its own number line. Stronger connections belong farther left, nearer the reference.'));
    body.append(element('p', '', 'Drag a photo, select one and click the number line, or use Tab and the arrow keys. Shift changes the distance in larger steps. Only the bottom ruler is numbered. The 0-100 scale shows relative distance based on shared-link similarity, not a percentage.'));
    body.append(element('p', '', 'Reveal awards points from top to bottom, showing each green target circle and dotted connector with its points. Your photos stay exactly where you placed them. Green target circles and dotted connectors show the reference distances. Each correct comparison splits its points between its two topics. The five awards add up to your score.'));
    body.append(element('p', '', 'Similarity compares shared outgoing Wikipedia links, adjusted for article size. The result panel shows shared-link counts and examples for each topic.'));
    const option = element('label', 'motion-option'); const checkbox = element('input'); checkbox.type = 'checkbox'; checkbox.checked = !!model?.reducedMotion; checkbox.disabled = !engine;
    checkbox.addEventListener('change', () => send('motion', { value: checkbox.checked ? 'reduced' : 'full' }));
    option.append(checkbox, element('span', '', 'Reduce motion')); body.append(option);
  });
  $('paths-button').addEventListener('click', () => {
    if (!model) return;
    const body = showDetails('Connections');
    body.append(element('p', '', 'Shared outgoing Wikipedia links, adjusted for article size.'));
    for (const row of $('path-rows').children) body.append(row.cloneNode(true));
    body.append(element('h3', '', 'Shortest article paths'));
    for (const index of model.order || [0, 1, 2, 3, 4]) {
      const topic = model.topics[index], row = element('div', 'path-row');
      row.append(element('strong', '', topic.label));
      const chain = element('div', 'path-chain');
      if (!topic.path?.length) chain.textContent = 'No directed path found in this collection.';
      else topic.path.forEach((title, step) => {
        if (step) chain.append(document.createTextNode(' \u2192 '));
        chain.append(link(title, topic.pathUrls[step]));
      });
      row.append(chain); body.append(row);
    }
  });
  $('sources-button').addEventListener('click', () => {
    if (!model) return;
    const body = showDetails('Sources and image credits');
    body.append(element('p', '', 'Article text and link data: Wikipedia contributors.'));
    body.append(link('Read ' + model.anchor, model.anchorUrl));
    const titles = new Set([model.anchorTitle, ...model.topics.map(t => t.title)]);
    for (const title of titles) {
      const credit = media.get(title); if (!credit) continue;
      const row = element('div', 'credit-entry'); row.append(element('h3', '', title));
      row.append(element('p', '', credit.author + ' · ' + credit.license)); row.append(link('Source image', credit.url));
      if (credit.licenseUrl) { row.append(document.createTextNode(' · ')); row.append(link('License', credit.licenseUrl)); }
      body.append(row);
    }
  });
  $('close-details').addEventListener('click', () => $('details').close());
  $('details').addEventListener('close', () => previousFocus?.focus());
  window.nearerView = {
    receive,
    attachEngine(instance) { engine = instance; if (model) receive(model); send('sync'); }
  };
  fetch('media.json').then(response => { if (!response.ok) throw Error('Media unavailable'); return response.json(); }).then(bank => {
    media = new Map(bank.entries.map(entry => [entry.title, entry]));
    if (anchorPortrait) updatePortrait(anchorPortrait); rows.forEach(row => updatePortrait(row.photo));
  }).catch(() => { /* Initials remain usable when a portrait is unavailable. */ });
}());
