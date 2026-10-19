mergeInto(LibraryManager.library, {
  NearerPixelRatio: function() {
    return window.nearerPixelRatio || 1;
  },
  NearerRead: function(key) {
    var value = '';
    try { value = localStorage.getItem(UTF8ToString(key)) || ''; } catch (_) {}
    var ptr = _malloc(lengthBytesUTF8(value) + 1);
    stringToUTF8(value, ptr, lengthBytesUTF8(value) + 1);
    return ptr;
  },
  NearerWrite: function(key, value) {
    try { localStorage.setItem(UTF8ToString(key), UTF8ToString(value)); return 1; } catch (_) { return 0; }
  },
  NearerMotionPreference: function() {
    return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 1 : 0;
  },
  NearerCopy: function(value) {
    var text = UTF8ToString(value);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function() { SendMessage('Nearer', 'OnClipboard', 'copied'); }, function() { SendMessage('Nearer', 'OnClipboard', 'unavailable'); });
    } else { SendMessage('Nearer', 'OnClipboard', 'unavailable'); }
  }
});
