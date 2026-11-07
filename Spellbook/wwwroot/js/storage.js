window.spellbookStorage = {
  get: key => localStorage.getItem(key),
  set: (key, value) => localStorage.setItem(key, value),
  download: (filename, content, contentType = 'application/json') => {
    const blob = new Blob([content], {type: contentType});
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = filename; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
};
