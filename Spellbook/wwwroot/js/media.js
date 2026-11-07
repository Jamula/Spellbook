// Local-only OCR adapter. No image bytes are sent to a server by this application.
// On first use the browser downloads third-party Tesseract.js code and English OCR data.
// The selected image stays in browser memory and is processed by a Web Worker.
window.spellbookMedia = (() => {
  const scriptUrl = "https://cdn.jsdelivr.net/npm/tesseract.js@5.1.1/dist/tesseract.min.js";
  let libraryPromise;
  function loadLibrary() {
    if (window.Tesseract) return Promise.resolve(window.Tesseract);
    if (libraryPromise) return libraryPromise;
    libraryPromise = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = scriptUrl;
      script.crossOrigin = "anonymous";
      script.onload = () => window.Tesseract ? resolve(window.Tesseract) : reject(new Error("OCR library unavailable"));
      script.onerror = () => reject(new Error("OCR library could not load"));
      document.head.appendChild(script);
    }).catch(e => { libraryPromise = undefined; throw e; });
    return libraryPromise;
  }
  async function recognize(imageDataUrl) {
    if (typeof imageDataUrl !== "string" || !imageDataUrl.startsWith("data:image/")) {
      throw new Error("Only local image previews are supported");
    }
    const tesseract = await loadLibrary();
    const worker = await tesseract.createWorker("eng");
    try {
      const result = await worker.recognize(imageDataUrl);
      return result?.data?.text || "";
    } finally {
      await worker.terminate();
    }
  }
  return { recognize };
})();
