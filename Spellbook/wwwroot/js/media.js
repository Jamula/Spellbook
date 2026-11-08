// Local-only OCR adapter. No image bytes are sent to a server by this application.
// On first use the browser downloads third-party Tesseract.js code and English OCR data.
// The selected image stays in browser memory and is processed by a Web Worker.
window.spellbookMedia = (() => {
  const scriptUrl = "https://cdn.jsdelivr.net/npm/tesseract.js@5.1.1/dist/tesseract.min.js";
  let libraryPromise;
  let workerPromise;
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
    if (!workerPromise) workerPromise = tesseract.createWorker("eng").catch(error => { workerPromise = undefined; throw error; });
    const worker = await workerPromise;
    const result = await worker.recognize(imageDataUrl);
    return result?.data?.text || "";
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
  let activeScanId;
  let cancelScan = false;
  function seek(video, time) {
    if (Math.abs(video.currentTime - time) < 0.04 && video.readyState >= 2 && !video.seeking) return Promise.resolve();
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => finish(new Error("Video frame timed out")), 15000);
      const finish = error => {
        clearTimeout(timeout);
        video.removeEventListener("seeked", done);
        video.removeEventListener("loadeddata", done);
        video.removeEventListener("canplay", done);
        video.removeEventListener("error", failed);
        video.removeEventListener("abort", failed);
        error ? reject(error) : resolve();
      };
      const done = () => {
        if (video.readyState >= 2 && !video.seeking) finish();
      };
      const failed = () => finish(new Error("Video frame could not be decoded"));
      video.addEventListener("seeked", done);
      video.addEventListener("loadeddata", done);
      video.addEventListener("canplay", done);
      video.addEventListener("error", failed);
      video.addEventListener("abort", failed);
      try {
        if (Math.abs(video.currentTime - time) >= 0.04) video.currentTime = time;
      } catch (error) {
        finish(error instanceof Error ? error : new Error("Video seek failed"));
        return;
      }
      done();
    });
  }
  function waitForVideoMetadata(video) {
    const ready = () => video.readyState >= 1 && Number.isFinite(video.duration) && video.duration > 0;
    if (ready()) return Promise.resolve();
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => finish(new Error("Video metadata timed out")), 15000);
      const finish = error => {
        clearTimeout(timeout);
        video.removeEventListener("loadedmetadata", check);
        video.removeEventListener("durationchange", check);
        video.removeEventListener("error", failed);
        video.removeEventListener("abort", failed);
        error ? reject(error) : resolve();
      };
      const check = () => {
        if (video.error) return finish(new Error("Video could not be decoded"));
        if (ready()) finish();
      };
      const failed = () => finish(new Error("Video metadata could not be loaded"));
      video.addEventListener("loadedmetadata", check);
      video.addEventListener("durationchange", check);
      video.addEventListener("error", failed);
      video.addEventListener("abort", failed);
      check();
    });
  }
  function estimateStackEdges(dataUrl) {
    return new Promise(resolve => {
      const image = new Image();
      image.onload = () => {
        const scale = Math.min(1, 240 / Math.max(image.naturalWidth, image.naturalHeight));
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
        canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
        const context = canvas.getContext("2d", { willReadFrequently: true });
        if (!context) return resolve(null);
        context.drawImage(image, 0, 0, canvas.width, canvas.height);
        const { width, height } = canvas;
        const pixels = context.getImageData(0, 0, width, height).data;
        const gray = new Float32Array(width * height);
        for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
          const p = (y * width + x) * 4;
          gray[y * width + x] = (pixels[p] * 299 + pixels[p + 1] * 587 + pixels[p + 2] * 114) / 1000;
        }
        let best = null;
        // Project edge energy at several orientations so the estimate does not require a fixed phone angle.
        for (let angle = 0; angle < 180; angle += 15) {
          const radians = angle * Math.PI / 180, nx = Math.cos(radians), ny = Math.sin(radians);
          const limit = Math.ceil(Math.abs(nx) * width + Math.abs(ny) * height);
          const profile = new Float32Array(limit * 2 + 1);
          for (let y = 1; y < height - 1; y += 2) for (let x = 1; x < width - 1; x += 2) {
            const i = y * width + x;
            const gx = (gray[i + 1] - gray[i - 1]) / 2;
            const gy = (gray[i + width] - gray[i - width]) / 2;
            const energy = Math.abs(gx * nx + gy * ny);
            if (energy < 24) continue;
            const r = Math.round(x * nx + y * ny) + limit;
            profile[r] += energy;
          }
          const smooth = new Float32Array(profile.length);
          for (let i = 2; i < profile.length - 2; i++) smooth[i] = (profile[i - 2] + profile[i - 1] + profile[i] + profile[i + 1] + profile[i + 2]) / 5;
          const mean = smooth.reduce((a, b) => a + b, 0) / smooth.length;
          const peaks = [];
          for (let i = 3; i < smooth.length - 3; i++) {
            if (smooth[i] > mean * 2.2 && smooth[i] >= smooth[i - 1] && smooth[i] >= smooth[i + 1] && (!peaks.length || i - peaks[peaks.length - 1] >= 3)) peaks.push(i);
          }
          if (peaks.length >= 3 && (!best || peaks.length > best.count)) best = { count: peaks.length };
        }
        // A weak visual heuristic only; face-on cards and unclear edges stay unavailable.
        resolve(best && best.count >= 4 ? { count: Math.max(2, Math.min(250, best.count)), confidence: "low" } : null);
      };
      image.onerror = () => resolve(null);
      image.src = dataUrl;
    });
  }
  async function scanVideo(videoId, callback, generation) {
    const video = document.getElementById(videoId);
    if (!video) throw new Error("Selected video preview is unavailable");
    await waitForVideoMetadata(video);
    if (!Number.isFinite(video.duration) || video.duration <= 0) throw new Error("Video duration is unavailable");
    const interval = Math.max(0.75, video.duration / 100);
    const times = [];
    for (let t = 0; t < video.duration && times.length < 100; t += interval) times.push(t);
    if (times.length < 100 && times.length && times[times.length - 1] < video.duration - 0.1) times.push(Math.max(0, video.duration - 0.05));
    activeScanId = videoId; cancelScan = false; video.pause();
    const frames = [];
    try {
      for (let i = 0; i < times.length; i++) {
        if (cancelScan || activeScanId !== videoId) break;
        await seek(video, times[i]);
        const image = captureFrame(videoId);
        const text = await recognize(image);
        const stack = text.trim() ? await estimateStackEdges(image) : null;
        frames.push({ time: times[i], text, stackCountEstimate: stack?.count ?? null, stackConfidence: stack?.confidence ?? "unavailable", sampleIndex: i });
        if (callback) await callback.invokeMethodAsync("OnVideoScanProgress", generation, i + 1, times.length);
      }
      return { frames, cancelled: cancelScan, sampledFrames: times.length };
    } finally { activeScanId = undefined; video.pause(); }
  }
  function cancelVideoScan() { cancelScan = true; }
  window.addEventListener("pagehide", releaseVideo);

  return { recognize, convertHeic, prepareVideo, captureFrame, scanVideo, cancelVideoScan, estimateStackEdges, releaseVideo };
})();
