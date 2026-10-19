(function () {
  'use strict';
  const $ = id => document.getElementById(id);
  let engine = null, model = null, puzzleId = '', rows = [], boxes = [], tray = null, anchorPortrait = null;
  let selectedTopic = -1, drag = null, ghost = null;
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
  function updateSelection() {
    rows.forEach((row, index) => { row.card.classList.toggle('selected', index === selectedTopic); row.card.setAttribute('aria-pressed', String(index === selectedTopic)); });
    boxes.forEach(box => box.root.classList.toggle('ready-to-place', selectedTopic >= 0 && !model?.revealed));
    const returnButton = document.querySelector('.return-topic'); if (returnButton) returnButton.hidden = selectedTopic < 0 || model?.revealed;
  }
  function chooseTopic(index) {
    if (!model || model.revealed || model.animating || !engine) return;
    selectedTopic = selectedTopic === index ? -1 : index; updateSelection();
    if (selectedTopic >= 0) send('select', { index });
  }
  function assign(index, slot) {
    if (!model || model.revealed || model.animating || !engine || index < 0) return;
    const slots = model.topics.map(topic => topic.slot);
    const old = slots[index], occupant = slot < 0 ? -1 : slots.indexOf(slot);
    if (occupant >= 0 && occupant !== index) slots[occupant] = old;
    slots[index] = slot;
    model.topics.forEach((topic, i) => { topic.slot = slots[i]; });
    selectedTopic = -1; renderBoard(model); send('rank', { slots });
  }
  function stopDrag() {
    if (drag) rows[drag.index]?.card.classList.remove('dragging-topic');
    ghost?.remove(); ghost = null; drag = null;
    boxes.forEach(box => box.root.classList.remove('drop-over'));
  }
  function createLanes(state) {
    stopDrag(); selectedTopic = -1; rows = new Array(5); boxes = [];
    $('lanes').replaceChildren(); $('anchor-portrait').replaceChildren();
    anchorPortrait = portrait($('anchor-portrait'), state.anchorTitle);
    const board = element('div', 'rank-board'); board.setAttribute('aria-label', 'Five ranks, most similar to least similar');
    for (let slot = 0; slot < 5; slot++) {
      const root = element('section', 'rank-slot'); root.dataset.rank = slot;
      const target = element('button', 'slot-target'); target.type = 'button';
      target.append(element('span', 'rank-number', String(slot + 1)), element('span', 'rank-caption', slot === 0 ? 'Most similar' : slot === 4 ? 'Least similar' : 'Rank ' + (slot + 1)));
      target.setAttribute('aria-label', 'Place selected topic in rank ' + (slot + 1) + (slot === 0 ? ', most similar' : slot === 4 ? ', least similar' : ''));
      const guess = element('div', 'slot-guess'), empty = element('span', 'slot-empty', 'Drop here'); guess.append(empty);
      const answer = element('div', 'slot-answer'); answer.hidden = true;
      answer.append(element('span', 'answer-caption', 'Correct placement'));
      const correctIndex = state.correctOrder[slot], correctTopic = state.topics[correctIndex];
      const photo = portrait(answer, correctTopic.title); answer.append(element('span', 'answer-name', correctTopic.label));
      root.append(target, guess, answer); board.append(root);
      root.addEventListener('click', event => {
        if (event.target.closest?.('.rank-topic') || model?.revealed) return;
        if (selectedTopic >= 0) assign(selectedTopic, slot);
        else { text($('round-status'), 'Select a topic first, then choose its box.'); rows.find(row => row.card.parentElement === tray)?.card.focus(); }
      });
      boxes.push({ root, target, guess, empty, answer, photo });
    }
    const traySection = element('section', 'tray-section');
    const trayHeader = element('div', 'tray-header'); trayHeader.append(element('h2', '', 'Your topics'), element('p', '', 'Drag a topic into a box, or select a topic and then a box.'));
    tray = element('div', 'topic-tray'); tray.setAttribute('aria-label', 'Topics waiting to be ranked');
    const returnButton = element('button', 'return-topic', 'Return selected topic'); returnButton.type = 'button';
    returnButton.addEventListener('click', () => { if (selectedTopic >= 0) assign(selectedTopic, -1); });
    trayHeader.append(returnButton); traySection.append(trayHeader, tray); $('lanes').append(board, traySection);
    (state.order || [0, 1, 2, 3, 4]).forEach(index => {
      const topic = state.topics[index], card = element('button', 'rank-topic'); card.type = 'button'; card.dataset.index = index;
      card.setAttribute('aria-label', topic.label + '. Select, then choose a rank; or use keys 1 to 5.');
      const photo = portrait(card, topic.title); card.append(element('span', 'rank-topic-name', topic.label));
      const badge = element('span', 'rank-award'); badge.hidden = true; card.append(badge);
      let suppressClick = false;
      card.addEventListener('click', () => { if (suppressClick) { suppressClick = false; return; } chooseTopic(index); });
      card.addEventListener('keydown', event => {
        if (model?.revealed || model?.animating) return;
        if (/^[1-5]$/.test(event.key)) { event.preventDefault(); assign(index, Number(event.key) - 1); }
        else if (event.key === 'Backspace' || event.key === 'Delete') { event.preventDefault(); assign(index, -1); }
        else if (event.key === 'Escape') { selectedTopic = -1; updateSelection(); stopDrag(); }
      });
      card.addEventListener('pointerdown', event => {
        if ((event.button !== undefined && event.button !== 0) || !engine || model?.revealed || model?.animating) return;
        drag = { index, pointer: event.pointerId, x: event.clientX, y: event.clientY, moved: false };
        card.setPointerCapture?.(event.pointerId);
      });
      card.addEventListener('pointermove', event => {
        if (!drag || drag.index !== index || drag.pointer !== event.pointerId) return;
        if (!drag.moved && Math.hypot(event.clientX - drag.x, event.clientY - drag.y) > 6) {
          drag.moved = true; ghost = card.cloneNode(true); ghost.classList.add('drag-ghost'); ghost.setAttribute('aria-hidden', 'true'); ghost.tabIndex = -1; document.body.append(ghost); card.classList.add('dragging-topic');
        }
        if (!drag.moved) return;
        event.preventDefault(); ghost.style.left = event.clientX + 12 + 'px'; ghost.style.top = event.clientY + 12 + 'px';
        const over = document.elementFromPoint?.(event.clientX, event.clientY)?.closest?.('[data-rank]');
        boxes.forEach(box => box.root.classList.toggle('drop-over', box.root === over));
      });
      card.addEventListener('pointerup', event => {
        if (!drag || drag.index !== index || drag.pointer !== event.pointerId) return;
        if (drag.moved) {
          suppressClick = true;
          const over = document.elementFromPoint?.(event.clientX, event.clientY);
          const slot = over?.closest?.('[data-rank]');
          if (slot) assign(index, Number(slot.dataset.rank));
          else if (over?.closest?.('.topic-tray')) assign(index, -1);
        }
        stopDrag();
      });
      card.addEventListener('pointercancel', stopDrag); card.addEventListener('lostpointercapture', stopDrag);
      rows[index] = { card, photo, badge }; tray.append(card);
    });
    pathsPuzzle = ''; pathSelection = null;
  }
  function renderBoard(state) {
    rows.forEach((row, index) => {
      const topic = state.topics[index], parent = topic.slot >= 0 ? boxes[topic.slot].guess : tray;
      if (row.card.parentElement !== parent) parent.append(row.card);
      row.card.disabled = !engine || state.animating || state.revealed;
      row.card.setAttribute('aria-label', topic.label + (topic.slot >= 0 ? ', rank ' + (topic.slot + 1) : ', not yet ranked') + (state.revealed ? ', ' + topic.points + ' points' : '. Select, then choose a rank; or use keys 1 to 5.'));
      text(row.badge, '+' + topic.points + ' points' + (topic.points === 10 ? ' - one away' : '')); row.badge.hidden = !state.revealed || !topic.awarded;
      row.card.classList.toggle('placement-correct', state.revealed && topic.awarded && topic.points === 20);
      row.card.classList.toggle('placement-close', state.revealed && topic.awarded && topic.points === 10);
      row.card.classList.toggle('placement-incorrect', state.revealed && topic.awarded && topic.points === 0);
    });
    boxes.forEach((box, slot) => {
      const index = state.topics.findIndex(topic => topic.slot === slot), topic = state.topics[index];
      box.empty.hidden = index >= 0; box.target.disabled = !engine || state.revealed || state.animating;
      box.answer.hidden = !state.revealed || !topic?.awarded;
      text(box.answer.querySelector('.answer-caption'), topic?.points === 20 && index !== state.correctOrder[slot] ? 'Near tie: either fits' : 'Correct placement');
      box.root.classList.toggle('revealed-slot', !box.answer.hidden);
    });
    document.querySelector('.tray-section').hidden = state.revealed;
    document.querySelector('.return-topic').hidden = selectedTopic < 0 || state.revealed;
    updateSelection();
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
    text($('result-score'), state.score + ' / 100 points' + (state.animating ? '' : ' · ' + state.correct + ' of ' + state.total + ' boxes correct'));
    if (state.revealed) selectedTopic = -1;
    renderBoard(state);
    text($('main-action'), state.animating ? (state.correcting ? 'Correcting…' : 'Counting points…') : state.revealed ? 'Next' : 'Reveal');
    $('main-action').disabled = !engine || state.animating || (!state.revealed && state.topics.some(topic => topic.slot < 0));
    $('share-action').hidden = !state.revealed || state.animating;
    $('paths-button').hidden = !state.revealed || state.animating;
    text($('round-status'), state.notice || (state.animating ? 'Revealing the correct order, from most similar to least similar.' : state.revealed ? '20 points for the right box, 10 for one box away. Near ties accept either order.' : 'Fill all five boxes. Swapping topics is fine; each box holds one.'));
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
    body.append(element('p', '', 'Place the five topics in order: most similar in box 1, least similar in box 5. We compare the links their Wikipedia pages share, adjusted for page size.'));
    body.append(element('p', '', 'Drag a circle and its name into a box. You can also select a topic and then a box, or focus a topic and press 1 to 5. Dropping onto an occupied box swaps the two topics. Delete returns a topic to the tray.'));
    body.append(element('p', '', 'The right box earns 20 points; one box away earns 10; farther away earns 0. Near ties accept either order. Reveal shows one correct order, one box at a time, then your score and personal stats.'));
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
    if (anchorPortrait) updatePortrait(anchorPortrait); rows.forEach(row => updatePortrait(row.photo)); boxes.forEach(box => updatePortrait(box.photo));
  }).catch(() => { /* Initials remain usable when a portrait is unavailable. */ });
}());
