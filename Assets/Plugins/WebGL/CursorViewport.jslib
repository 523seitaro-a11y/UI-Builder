mergeInto(LibraryManager.library, {
  CursorViewport_Install: function (
    imageUrlPtr,
    referenceCursorSize,
    referenceViewportHeight,
    normalizedHotspotX,
    normalizedHotspotY
  ) {
    if (Module.cursorViewportOverlay) {
      Module.cursorViewportOverlay.remove();
    }

    var canvas = Module["canvas"];
    if (!canvas) {
      return;
    }

    var image = document.createElement("img");
    image.src = UTF8ToString(imageUrlPtr);
    image.alt = "";
    image.draggable = false;
    image.style.position = "fixed";
    image.style.left = "0";
    image.style.top = "0";
    image.style.pointerEvents = "none";
    image.style.userSelect = "none";
    image.style.zIndex = "2147483647";
    image.style.display = "none";
    image.style.visibility = "visible";
    image.style.willChange = "transform";
    image.style.transform = "translate3d(-10000px,-10000px,0)";
    document.body.appendChild(image);

    var originX = 0;
    var originY = 0;
    var coordinateScaleX = 1;
    var coordinateScaleY = 1;
    var hotspotX = 0;
    var hotspotY = 0;
    var lastClientX = 0;
    var lastClientY = 0;
    var hasPointerPosition = false;

    var applyPosition = function () {
      if (!hasPointerPosition) {
        return;
      }

      var localX =
        (lastClientX - originX) / coordinateScaleX - hotspotX;
      var localY =
        (lastClientY - originY) / coordinateScaleY - hotspotY;

      image.style.transform =
        "translate3d(" + localX + "px," + localY + "px,0)";
    };

    var updateLayout = function () {
      var wasDisplayed = image.style.display !== "none";

      image.style.visibility = "hidden";
      image.style.display = "block";
      image.style.width = "100px";
      image.style.height = "100px";
      image.style.transform = "translate3d(0,0,0)";

      var probeRect = image.getBoundingClientRect();
      originX = probeRect.left;
      originY = probeRect.top;
      coordinateScaleX =
        probeRect.width > 0 ? probeRect.width / 100 : 1;
      coordinateScaleY =
        probeRect.height > 0 ? probeRect.height / 100 : 1;

      var canvasHeight = canvas.getBoundingClientRect().height;
      var screenSize = Math.max(
        1,
        referenceCursorSize * canvasHeight / referenceViewportHeight
      );
      var localWidth = screenSize / coordinateScaleX;
      var localHeight = screenSize / coordinateScaleY;

      image.style.width = localWidth + "px";
      image.style.height = localHeight + "px";
      hotspotX = localWidth * normalizedHotspotX;
      hotspotY = localHeight * normalizedHotspotY;

      applyPosition();
      image.style.display = wasDisplayed ? "block" : "none";
      image.style.visibility = "visible";
    };

    var updatePosition = function (event) {
      lastClientX = event.clientX;
      lastClientY = event.clientY;
      hasPointerPosition = true;
      canvas.style.cursor = "none";
      image.style.display = "block";
      applyPosition();
    };

    var show = function (event) {
      canvas.style.cursor = "none";
      image.style.display = "block";

      if (typeof event.clientX === "number") {
        lastClientX = event.clientX;
        lastClientY = event.clientY;
        hasPointerPosition = true;
        applyPosition();
      }
    };

    var hide = function () {
      image.style.display = "none";
    };

    // pointerrawupdate uses a different coordinate space in some embedded
    // WebGL hosts. pointermove keeps clientX/Y in CSS viewport coordinates.
    var pointerEventName = "pointermove";

    canvas.addEventListener(pointerEventName, updatePosition, {
      passive: true
    });
    canvas.addEventListener("pointerenter", show, { passive: true });
    canvas.addEventListener("pointerleave", hide, { passive: true });
    window.addEventListener("resize", updateLayout, { passive: true });
    window.addEventListener("scroll", updateLayout, {
      passive: true,
      capture: true
    });

    var resizeObserver = null;
    if (typeof ResizeObserver !== "undefined") {
      resizeObserver = new ResizeObserver(updateLayout);
      resizeObserver.observe(canvas);
    }

    updateLayout();
    canvas.style.cursor = "none";

    Module.cursorViewportOverlay = {
      remove: function () {
        canvas.removeEventListener(pointerEventName, updatePosition);
        canvas.removeEventListener("pointerenter", show);
        canvas.removeEventListener("pointerleave", hide);
        window.removeEventListener("resize", updateLayout);
        window.removeEventListener("scroll", updateLayout, true);

        if (resizeObserver) {
          resizeObserver.disconnect();
        }

        image.remove();
        canvas.style.cursor = "default";
      }
    };
  },

  CursorViewport_Remove: function () {
    if (!Module.cursorViewportOverlay) {
      return;
    }

    Module.cursorViewportOverlay.remove();
    Module.cursorViewportOverlay = null;
  }
});
