(() => {
  const runtime = {};
  const imageExtensions = [
    "png", "jpg", "jpeg", "webp", "gif", "avif", "svg", "bmp", "ico",
    "apng", "tif", "tiff", "jfif", "pjpeg", "pjp", "heic", "heif",
    "jxl", "jp2", "j2k", "jpf", "jpx", "jpm", "mj2", "dds", "tga",
    "psd", "raw", "cr2", "nef", "orf", "arw"
  ];

  const normalizeUrl = value => {
    if (!value || typeof value !== "string") return "";
    const trimmed = value.trim();
    if (!trimmed || trimmed.startsWith("data:") || trimmed.startsWith("blob:")) return trimmed;
    try { return new URL(trimmed, document.baseURI).href; } catch { return trimmed; }
  };

  const formatFromUrl = url => {
    if (!url) return "unknown";
    const dataMatch = /^data:image\/([^;,]+)/i.exec(url);
    if (dataMatch) return dataMatch[1].toLowerCase();
    try {
      const path = new URL(url, document.baseURI).pathname.toLowerCase();
      const extension = path.includes(".") ? path.split(".").pop() : "";
      return imageExtensions.includes(extension) ? extension : "unknown";
    } catch {
      const clean = url.split("?")[0].split("#")[0].toLowerCase();
      const extension = clean.includes(".") ? clean.split(".").pop() : "";
      return imageExtensions.includes(extension) ? extension : "unknown";
    }
  };

  const readAttributes = element => {
    const attributes = {};
    for (const attribute of element.attributes || []) {
      attributes[attribute.name] = attribute.value;
    }
    return attributes;
  };

  const readComputedStyle = element => {
    const computed = getComputedStyle(element);
    const computedStyle = {};
    for (const name of computed) {
      computedStyle[name] = computed.getPropertyValue(name);
    }
    return computedStyle;
  };

  const buildSelector = (element, fallbackSelector) => {
    if (element.id && typeof CSS !== "undefined" && CSS.escape) return `#${CSS.escape(element.id)}`;
    if (fallbackSelector && fallbackSelector !== "lens center") return fallbackSelector;

    const escape = value => typeof CSS !== "undefined" && CSS.escape ? CSS.escape(value) : value.replace(/[^a-zA-Z0-9_-]/g, "\\$&");
    const parts = [];
    let current = element;
    while (current && current.nodeType === Node.ELEMENT_NODE && parts.length < 5) {
      let part = current.localName;
      if (current.classList.length > 0) {
        part += "." + Array.from(current.classList).slice(0, 2).map(escape).join(".");
      }

      const parent = current.parentElement;
      if (parent) {
        const siblings = Array.from(parent.children).filter(item => item.localName === current.localName);
        if (siblings.length > 1) {
          part += `:nth-of-type(${siblings.indexOf(current) + 1})`;
        }
      }

      parts.unshift(part);
      current = parent;
    }

    return parts.join(" > ") || fallbackSelector || element.localName;
  };

  const readMatchedCssRules = element => {
    const matchedCssRules = [];
    const visitRules = rules => {
      for (const rule of rules) {
        if (rule.cssRules) {
          try { visitRules(rule.cssRules); } catch {}
        }

        if (!rule.selectorText) continue;
        try {
          if (element.matches(rule.selectorText)) matchedCssRules.push(rule.selectorText);
        } catch {}
      }
    };

    for (const sheet of document.styleSheets) {
      try { visitRules(sheet.cssRules); } catch {}
    }

    return Array.from(new Set(matchedCssRules)).slice(0, 200);
  };

  const calculateChromeOffset = () => {
    const chromeLeft = Math.max(0, (window.outerWidth - window.innerWidth) / 2);
    const chromeTop = Math.max(0, window.outerHeight - window.innerHeight - chromeLeft);
    return { chromeLeft, chromeTop };
  };

  const toViewport = (screenX, screenY) => {
    const { chromeLeft, chromeTop } = calculateChromeOffset();
    return {
      x: screenX - window.screenX - chromeLeft,
      y: screenY - window.screenY - chromeTop
    };
  };

  const toScreenRect = rect => {
    const { chromeLeft, chromeTop } = calculateChromeOffset();
    return {
      x: window.screenX + chromeLeft + rect.left,
      y: window.screenY + chromeTop + rect.top,
      width: rect.width,
      height: rect.height
    };
  };

  const calculateRegion = (regionLeft, regionTop, regionWidth, regionHeight) => {
    const topLeft = toViewport(regionLeft, regionTop);
    const bottomRight = toViewport(regionLeft + regionWidth, regionTop + regionHeight);
    return {
      left: Math.min(topLeft.x, bottomRight.x),
      top: Math.min(topLeft.y, bottomRight.y),
      right: Math.max(topLeft.x, bottomRight.x),
      bottom: Math.max(topLeft.y, bottomRight.y)
    };
  };

  const findScreenPointHit = (screenX, screenY) => {
    const { chromeLeft, chromeTop } = calculateChromeOffset();
    const dpr = window.devicePixelRatio || 1;
    const candidates = [
      { screenX, screenY, mode: "raw" },
      { screenX: screenX / dpr, screenY: screenY / dpr, mode: "devicePixelRatio" }
    ];

    const mapCandidate = candidate => {
      const viewportX = candidate.screenX - window.screenX - chromeLeft;
      const viewportY = candidate.screenY - window.screenY - chromeTop;
      const inside = viewportX >= 0
        && viewportY >= 0
        && viewportX <= window.innerWidth
        && viewportY <= window.innerHeight;
      return {
        ...candidate,
        viewportX,
        viewportY,
        inside,
        element: inside ? document.elementFromPoint(viewportX, viewportY) : null
      };
    };

    return candidates.map(mapCandidate).find(candidate => candidate.element)
      ?? candidates
        .map(candidate => {
          const viewportX = candidate.screenX - window.screenX - chromeLeft;
          const viewportY = candidate.screenY - window.screenY - chromeTop;
          return {
            ...candidate,
            viewportX,
            viewportY,
            inside: false,
            element: document.elementFromPoint(viewportX, viewportY)
          };
        })
        .find(candidate => candidate.element);
  };

  const collectImages = (element, region, viewportX, viewportY) => {
    const images = [];
    const seenImages = new Map();
    const scoreImageElement = imageElement => {
      if (!imageElement?.getBoundingClientRect || !region) return imageElement === element ? 100000000 : 0;
      const rect = imageElement.getBoundingClientRect();
      const overlapWidth = Math.max(0, Math.min(rect.right, region.right) - Math.max(rect.left, region.left));
      const overlapHeight = Math.max(0, Math.min(rect.bottom, region.bottom) - Math.max(rect.top, region.top));
      const overlapArea = overlapWidth * overlapHeight;
      const centerX = Number.isFinite(viewportX) ? viewportX : (region.left + region.right) / 2;
      const centerY = Number.isFinite(viewportY) ? viewportY : (region.top + region.bottom) / 2;
      const centerInside = centerX >= rect.left && centerX <= rect.right && centerY >= rect.top && centerY <= rect.bottom;
      const elementBonus = imageElement === element ? 100000000 : 0;
      const centerBonus = centerInside ? 10000000 : 0;
      return elementBonus + centerBonus + overlapArea;
    };

    const addImage = (url, kind, imageElement, alt = "") => {
      const normalized = normalizeUrl(url);
      if (!normalized) return;
      const format = formatFromUrl(normalized);
      if (format === "unknown" && !normalized.startsWith("blob:")) return;
      const image = {
        url: normalized,
        kind,
        format,
        alt: alt || imageElement?.getAttribute?.("alt") || imageElement?.getAttribute?.("aria-label") || "",
        width: Math.round(imageElement?.naturalWidth || imageElement?.videoWidth || imageElement?.clientWidth || 0),
        height: Math.round(imageElement?.naturalHeight || imageElement?.videoHeight || imageElement?.clientHeight || 0),
        naturalWidth: Math.round(imageElement?.naturalWidth || imageElement?.videoWidth || 0),
        naturalHeight: Math.round(imageElement?.naturalHeight || imageElement?.videoHeight || 0),
        displayedWidth: Math.round(imageElement?.getBoundingClientRect?.().width || imageElement?.clientWidth || 0),
        displayedHeight: Math.round(imageElement?.getBoundingClientRect?.().height || imageElement?.clientHeight || 0),
        isAnimated: format === "gif" || format === "apng" || format === "webp",
        priority: scoreImageElement(imageElement)
      };
      const existingIndex = seenImages.get(normalized);
      if (existingIndex !== undefined) {
        if ((images[existingIndex].priority || 0) < image.priority) images[existingIndex] = image;
        return;
      }

      seenImages.set(normalized, images.length);
      images.push(image);
    };

    const addSrcSet = (srcset, kind, imageElement) => {
      if (!srcset) return;
      for (const candidate of srcset.split(",")) addImage(candidate.trim().split(/\s+/)[0], kind, imageElement);
    };

    const addCssImages = (cssValue, imageElement) => {
      if (!cssValue || cssValue === "none") return;
      for (const match of cssValue.matchAll(/url\((['"]?)(.*?)\1\)/g)) addImage(match[2], "css-image", imageElement);
    };

    const imageRoots = region
      ? Array.from(document.querySelectorAll("*")).filter(item => {
        const rect = item.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return item === element;
        return rect.right >= region.left
          && rect.left <= region.right
          && rect.bottom >= region.top
          && rect.top <= region.bottom;
      })
      : [element, ...element.querySelectorAll("*")];
    if (!imageRoots.includes(element)) imageRoots.unshift(element);

    for (const item of imageRoots) {
      const tag = item.localName;
      if (tag === "img" || tag === "image") {
        addImage(item.currentSrc || item.src || item.href?.baseVal, tag, item);
        addSrcSet(item.srcset, "srcset", item);
      }
      if (tag === "source") {
        addImage(item.src, "source", item);
        addSrcSet(item.srcset, "source-srcset", item);
      }
      if (tag === "video") addImage(item.poster, "video-poster", item);
      const style = getComputedStyle(item);
      addCssImages(style.backgroundImage, item);
      addCssImages(style.borderImageSource, item);
      addCssImages(style.listStyleImage, item);
      addCssImages(style.content, item);
    }

    return images.sort((a, b) => (b.priority || 0) - (a.priority || 0));
  };

  const buildLensMatch = (element, images, region, viewportX, viewportY) => {
    if (!element) return { confidence: "fallback", reason: "No element was available at the lens center." };
    const tag = (element.localName || "").toLowerCase();
    const rect = element.getBoundingClientRect?.();
    const pointInsideElement = rect
      && Number.isFinite(viewportX)
      && Number.isFinite(viewportY)
      && viewportX >= rect.left
      && viewportX <= rect.right
      && viewportY >= rect.top
      && viewportY <= rect.bottom;
    const regionArea = region ? Math.max(1, (region.right - region.left) * (region.bottom - region.top)) : 1;
    const elementArea = rect ? Math.max(1, rect.width * rect.height) : 1;
    const sizeRatio = Math.min(regionArea, elementArea) / Math.max(regionArea, elementArea);
    const topImage = images?.[0];

    if ((tag === "img" || tag === "image" || tag === "canvas" || tag === "svg") && pointInsideElement) {
      return { confidence: "exact image", reason: "The lens center is inside a direct visual element." };
    }

    if (topImage && (topImage.priority || 0) >= 10000000) {
      return { confidence: "nearest image", reason: "A visual resource overlaps the lens center or frame." };
    }

    if (pointInsideElement && sizeRatio > 0.55) {
      return { confidence: "container", reason: "The detected element closely matches the framed area." };
    }

    if (pointInsideElement) {
      return { confidence: "container", reason: "The lens center is inside this element, but the frame covers a broader area." };
    }

    return { confidence: "fallback", reason: "Julco used the nearest inspectable element from the lens center." };
  };

  runtime.inspectElement = (element, fallbackSelector, region = null, viewportX = Number.NaN, viewportY = Number.NaN) => {
    if (!element) {
      return {
        found: false,
        viewportX,
        viewportY,
        message: "No element matched the selected target."
      };
    }

    const rect = element.getBoundingClientRect();
    const images = collectImages(element, region, viewportX, viewportY);
    return {
      found: true,
      viewportX,
      viewportY,
      selector: buildSelector(element, fallbackSelector),
      tagName: element.tagName,
      attributes: readAttributes(element),
      outerHtml: element.outerHTML,
      computedStyle: readComputedStyle(element),
      matchedCssRules: readMatchedCssRules(element),
      images,
      elementBounds: toScreenRect(rect),
      lensMatch: buildLensMatch(element, images, region, viewportX, viewportY)
    };
  };

  runtime.inspectSelector = selector => {
    return runtime.inspectElement(document.querySelector(selector), selector);
  };

  runtime.inspectScreenPoint = (screenX, screenY, regionLeft, regionTop, regionWidth, regionHeight) => {
    const hit = findScreenPointHit(screenX, screenY);
    const region = calculateRegion(regionLeft, regionTop, regionWidth, regionHeight);
    return runtime.inspectElement(hit?.element ?? null, "lens center", region, hit?.viewportX ?? Number.NaN, hit?.viewportY ?? Number.NaN);
  };

  window.__julcoInspectionRuntime = runtime;
  return "ready";
})()
