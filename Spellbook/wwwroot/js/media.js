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

  // Only the user-selected local File is read. A conversion library may be
  // downloaded if Safari cannot decode HEIC itself; image bytes are never uploaded.
  const heicScript = "https://cdn.jsdelivr.net/npm/heic2any@0.0.4/dist/heic2any.min.js";
  let heicPromise;
  let activeVideoUrl;
  function selectedFile(inputId, predicate) {
    const picker = document.getElementById(inputId);
    const file = Array.from(picker?.files || []).find(predicate);
    if (!file) throw new Error("No matching local media file selected");
    return file;
  }
  function releaseVideo() {
    if (activeVideoUrl) URL.revokeObjectURL(activeVideoUrl);
    activeVideoUrl = undefined;
  }
  function heicFile(file) {
    return /^image\/hei[cf]$/i.test(file.type) || /\.(hei[cf])$/i.test(file.name);
  }
  function videoFile(file) {
    return file.type.startsWith("video/") || /\.(mov|mp4|m4v|webm)$/i.test(file.name);
  }
  function loadHeic() {
    if (window.heic2any) return Promise.resolve(window.heic2any);
    if (!heicPromise) {
      heicPromise = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = heicScript;
        script.crossOrigin = "anonymous";
        script.onload = () => window.heic2any ? resolve(window.heic2any) : reject(new Error("HEIC decoder unavailable"));
        script.onerror = () => reject(new Error("HEIC decoder could not load"));
        document.head.appendChild(script);
      }).catch(error => { heicPromise = undefined; throw error; });
    }
    return heicPromise;
  }
  async function jpegPreview(blob) {
    const url = URL.createObjectURL(blob);
    try {
      const img = new Image();
      await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = () => reject(new Error("This image cannot be decoded in your browser"));
        img.src = url;
      });
      const longest = Math.max(img.naturalWidth, img.naturalHeight);
      if (!longest) throw new Error("Image has no visible pixels");
      const factor = Math.min(1, 1600 / longest);
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(img.naturalWidth * factor));
      canvas.height = Math.max(1, Math.round(img.naturalHeight * factor));
      const ctx = canvas.getContext("2d");
      if (!ctx) throw new Error("Canvas is not available");
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
      return canvas.toDataURL("image/jpeg", 0.82);
    } finally {
      URL.revokeObjectURL(url);
    }
  }
  async function convertHeic(inputId) {
    const file = selectedFile(inputId, heicFile);
    if (file.size > 20_000_000) throw new Error("HEIC image exceeds the 20 MB limit");
    try {
      return await jpegPreview(file); // Safari versions with native HEIC support
    } catch {
      const convert = await loadHeic();
      const output = await convert({ blob: file, toType: "image/jpeg", quality: 0.82 });
      return jpegPreview(Array.isArray(output) ? output[0] : output);
    }
  }
  function prepareVideo(inputId) {
    const file = selectedFile(inputId, videoFile);
    if (file.size > 250_000_000) throw new Error("Video exceeds the 250 MB limit");
    releaseVideo();
    activeVideoUrl = URL.createObjectURL(file);
    return activeVideoUrl;
  }
  function captureFrame(videoId) {
    const video = document.getElementById(videoId);
    if (!video || !video.videoWidth || video.readyState < 2 || video.seeking)
      throw new Error("Wait until the selected video frame has loaded before capturing");
    video.pause();
    const factor = Math.min(1, 1600 / Math.max(video.videoWidth, video.videoHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(video.videoWidth * factor));
    canvas.height = Math.max(1, Math.round(video.videoHeight * factor));
    const context = canvas.getContext("2d");
    if (!context) throw new Error("Canvas is not available");
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL("image/jpeg", 0.82);
  }
  window.addEventListener("pagehide", releaseVideo);

  return { recognize, convertHeic, prepareVideo, captureFrame, releaseVideo };
})();
