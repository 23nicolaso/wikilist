(function () {
  'use strict';
  const $ = id => document.getElementById(id);
  let engine = null, model = null, puzzleId = '', rows = [], anchorPortrait = null;
  let selectedTopic = -1, drag = null;
  let media = new Map(), pathSelection = null, pathsPuzzle = '', previousFocus = null;
  let audioContext = null;
  const soundBuffers = new Map();
  const soundFiles = { start: 'Sounds/score-click.wav', complete: 'Sounds/score-complete.wav', next: 'Sounds/round-next.wav' };
  function prepareAudio() {
    try {
      const Context = window.AudioContext || window.webkitAudioContext;
      if (!Context) return null;
      if (!audioContext) audioContext = new Context();
      // Resume in the Reveal button's user gesture so the later completion can play.
      const resumed = audioContext.resume(); resumed?.catch(() => {});
      for (const [name, file] of Object.entries(soundFiles)) {
        if (!soundBuffers.has(name)) soundBuffers.set(name, fetch(file).then(response => {
          if (!response.ok) throw Error('Sound unavailable'); return response.arrayBuffer();
        }).then(bytes => audioContext.decodeAudioData(bytes)).catch(() => null));
      }
      return audioContext;
    } catch (_) { return null; }
  }
  function playSound(name) {
    const context = prepareAudio(); if (!context) return;
    soundBuffers.get(name)?.then(buffer => {
      if (!buffer || context.state !== 'running') return;
      const source = context.createBufferSource(), gain = context.createGain();
      source.buffer = buffer; gain.gain.value = name === 'start' ? 0.55 : 0.65;
      source.connect(gain); gain.connect(context.destination); source.start();
    }).catch(() => {});
  }
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
    const existing = rounds.find(round => round.id === state.puzzleId);
    if (existing?.score === state.score) return;
    if (existing) existing.score = state.score;
    else rounds.push({ id: state.puzzleId, score: state.score });
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
  const innerOrbit = 110, outerOrbit = 232;
  function point(topic, reference = false) {
    const magnitude = Math.hypot(topic.x, topic.y), radius = innerOrbit + fraction(reference ? topic.reference : topic.distance) * (outerOrbit - innerOrbit);
    return { x: 300 + (magnitude ? topic.x / magnitude : 0) * radius, y: 300 + (magnitude ? topic.y / magnitude : -1) * radius };
  }
  function stopDrag() { drag = null; }
  function placeOrbit(index, x, y) {
    if (!engine || !model || model.revealed || model.animating) return;
    const length = Math.hypot(x, y), distance = fraction((length - innerOrbit) / (outerOrbit - innerOrbit));
    const magnitude = 0.18 + distance * 0.79;
    const px = length > 0.001 ? x / length * magnitude : 0, py = length > 0.001 ? y / length * magnitude : -magnitude;
    model.topics[index].x = px; model.topics[index].y = py; model.topics[index].distance = distance;
    renderBoard(model); send('circle', { index, x: px, y: py });
  }
  function stagePoint(event) {
    const bounds = $('lanes').getBoundingClientRect();
    return { x: (event.clientX - bounds.left) / bounds.width * 600, y: (event.clientY - bounds.top) / bounds.height * 600 };
  }
  function createLanes(state) {
    stopDrag(); selectedTopic = -1; rows = new Array(5); $('lanes').replaceChildren();
    const guides = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); guides.classList.add('orbit-guides'); guides.setAttribute('viewBox', '0 0 600 600'); guides.setAttribute('aria-hidden', 'true');
    [110, 150, 190, 232].forEach(radius => {
      const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
      circle.setAttribute('cx', '300'); circle.setAttribute('cy', '300'); circle.setAttribute('r', String(radius)); guides.append(circle);
    });
    const connectors = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); connectors.classList.add('orbit-connectors'); connectors.setAttribute('viewBox', '0 0 600 600'); connectors.setAttribute('aria-hidden', 'true');
    const center = element('div', 'orbit-center'); anchorPortrait = portrait(center, state.anchorTitle);
    const label = element('span', 'orbit-anchor', state.anchor); label.id = 'anchor-name'; center.append(label);
    $('lanes').append(guides, connectors, center);
    (state.order || [0, 1, 2, 3, 4]).forEach(index => {
      const topic = state.topics[index];
      const line = document.createElementNS('http://www.w3.org/2000/svg', 'line'); line.classList.add('orbit-correction'); line.setAttribute('vector-effect', 'non-scaling-stroke'); connectors.append(line);
      const target = element('span', 'orbit-target'); target.hidden = true; target.setAttribute('aria-hidden', 'true'); target.title = topic.label + ': correct distance';
      const card = element('button', 'orbit-topic'); card.type = 'button'; card.dataset.index = index;
      const badge = element('span', 'orbit-award'); badge.hidden = true;
      const photo = portrait(card, topic.title); card.append(element('span', 'orbit-name', topic.label), badge);
      card.addEventListener('click', () => { selectedTopic = index; send('select', { index }); renderBoard(model); });
      card.addEventListener('pointerdown', event => {
        if ((event.button !== undefined && event.button !== 0) || !engine || model?.revealed || model?.animating) return;
        const cursor = stagePoint(event), current = point(model.topics[index]);
        drag = { index, pointer: event.pointerId, offsetX: current.x - cursor.x, offsetY: current.y - cursor.y };
        selectedTopic = index; send('select', { index }); card.setPointerCapture?.(event.pointerId);
      });
      card.addEventListener('pointermove', event => {
        if (!drag || drag.index !== index || drag.pointer !== event.pointerId) return;
        event.preventDefault(); const cursor = stagePoint(event);
        placeOrbit(index, cursor.x + drag.offsetX - 300, cursor.y + drag.offsetY - 300);
      });
      card.addEventListener('pointerup', stopDrag); card.addEventListener('pointercancel', stopDrag); card.addEventListener('lostpointercapture', stopDrag);
      card.addEventListener('keydown', event => {
        if (!model || model.revealed || model.animating) return;
        const current = point(model.topics[index]), step = event.shiftKey ? 24 : 8;
        let x = current.x - 300, y = current.y - 300;
        if (event.key === 'ArrowLeft') x -= step;
        else if (event.key === 'ArrowRight') x += step;
        else if (event.key === 'ArrowUp') y -= step;
        else if (event.key === 'ArrowDown') y += step;
        else if (event.key === 'Home' || event.key === 'End') { const ratio = (event.key === 'Home' ? innerOrbit : outerOrbit) / Math.hypot(x, y); x *= ratio; y *= ratio; }
        else return;
        event.preventDefault(); selectedTopic = index; placeOrbit(index, x, y);
      });
      $('lanes').append(target, card); rows[index] = { card, photo, badge, target, line };
    });
    pathsPuzzle = ''; pathSelection = null;
  }
  function renderBoard(state) {
    if (!state) return;
    rows.forEach((row, index) => {
      const topic = state.topics[index], guess = point(topic), target = point(topic, true);
      row.card.style.left = percent(guess.x / 600); row.card.style.top = percent(guess.y / 600);
      row.card.disabled = !engine || state.animating;
      row.card.classList.toggle('selected', selectedTopic === index);
      row.card.setAttribute('aria-label', topic.label + ', distance ' + Math.round(topic.distance * 100) + ' out of 100' + (state.revealed ? ', ' + topic.points + ' points' : '. Drag or use arrow keys.'));
      text(row.badge, '+' + topic.points + ' points'); row.badge.hidden = !state.revealed || !topic.awarded;
      row.target.style.left = percent(target.x / 600); row.target.style.top = percent(target.y / 600); row.target.hidden = !state.revealed || !topic.awarded;
      row.line.setAttribute('x1', String(guess.x)); row.line.setAttribute('y1', String(guess.y)); row.line.setAttribute('x2', String(target.x)); row.line.setAttribute('y2', String(target.y));
      row.line.style.display = state.revealed && topic.awarded && Math.abs(topic.reference - topic.distance) > 0.001 ? '' : 'none';
      row.line.style.opacity = selectedTopic < 0 || selectedTopic === index ? '1' : '0.55'; row.target.style.opacity = row.line.style.opacity;
      row.target.classList.toggle('selected-target', selectedTopic === index);
    });
  }
  function renderPaths(state) {
    const panel = $('paths-panel'); panel.hidden = !state.revealed || state.animating;
    if (panel.hidden || (pathsPuzzle === state.puzzleId && pathSelection === state.selected)) return;
    $('path-rows').replaceChildren();
    (state.correctOrder || [0, 1, 2, 3, 4]).forEach(index => {
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
      closeResults(); stopDrag();
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
    text($('result-score'), state.score + ' / 100 points' + (state.animating ? '' : ' · ' + state.correct + ' of ' + state.total + ' comparisons correct'));
    selectedTopic = state.selected;
    renderBoard(state);
    text($('main-action'), state.animating ? (state.correcting ? 'Correcting…' : 'Counting points…') : state.revealed ? 'Next' : 'Reveal');
    $('main-action').disabled = !engine || state.animating;
    $('share-action').hidden = !state.revealed || state.animating;
    $('paths-button').hidden = !state.revealed || state.animating;
    text($('round-status'), state.notice || (state.animating ? 'Revealing points and correct distances, clockwise.' : state.revealed ? 'Select a photo to highlight its green target circle. Points reward the order of your connections.' : 'Drag closer matches toward the center. Untouched topics count too.'));
    renderPaths(state);
    if (state.revealed && state.animating && previous?.puzzleId === state.puzzleId) {
      state.topics.forEach((topic, index) => {
        if (topic.awarded && !previous.topics[index].awarded) playSound('start');
      });
    }
    if (state.revealed && !state.animating && state.topics.every(topic => topic.awarded) && Number.isFinite(state.score) && state.score >= 0 && state.score <= 100) {
      recordRound(state);
      if (!popupRounds.has(state.puzzleId)) {
        popupRounds.add(state.puzzleId);
        if (previous?.puzzleId === state.puzzleId && previous.animating) { playSound('complete'); showResults(true); }
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
  function nextRound() {
    if (!engine || !model?.revealed || model.animating) return;
    playSound('next'); send('next');
  }
  $('next-result').addEventListener('click', () => { closeResults(); nextRound(); });
  $('results').addEventListener('close', () => resultFocus?.focus());
  $('main-action').addEventListener('click', () => {
    if (!model || model.animating || !engine) return;
    if (model.revealed) nextRound();
    else { prepareAudio(); send('reveal'); }
  });
  $('images-toggle').addEventListener('click', () => send('images'));
  $('share-action').addEventListener('click', () => send('share'));
  $('help-button').addEventListener('click', () => {
    const body = showDetails('A sense of connection');
    body.append(element('p', '', 'Move stronger connections nearer the center and weaker connections farther away. We compare shared Wikipedia links, adjusted for page size.'));
    body.append(element('p', '', 'Drag a photo, or focus it and use the arrow keys. Shift moves farther. Home and End move to the nearest and farthest distances.'));
    body.append(element('p', '', 'Untouched topics count too. Scoring rewards the order of your connections. Each correct comparison splits its points between the two topics. Near ties accept either order; the awards add up to 100 possible points.'));
    body.append(element('p', '', 'Reveal runs clockwise. Your photos stay where you put them; green dashed lines point to small green circles at the correct distances. Select a photo to highlight its correction. Best, average, and recent scores remain saved in this browser.'));
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
