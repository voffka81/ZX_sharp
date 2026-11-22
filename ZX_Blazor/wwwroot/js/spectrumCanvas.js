const displays = new WeakMap();

export function initialize(canvas, callback) {
    const context = canvas.getContext("2d", { alpha: false });
    if (!context) throw new Error("Canvas 2D context unavailable");
    context.imageSmoothingEnabled = false;
    const imageData = context.createImageData(canvas.width, canvas.height);

    const map = new Map([
        ["ShiftLeft", "CShift"], ["ShiftRight", "CShift"],
        ["ControlLeft", "SShift"], ["ControlRight", "SShift"],
        ["Enter", "Enter"], ["Space", "Space"],
        ...Array.from("ABCDEFGHIJKLMNOPQRSTUVWXYZ", x => ["Key" + x, x]),
        ...Array.from("0123456789", x => ["Digit" + x, "N" + x])
    ]);
    const pressed = new Set();
    const send = (code, down) => {
        const key = map.get(code);
        if (!key) return false;
        callback.invokeMethodAsync("OnKeyChanged", key, down);
        return true;
    };
    const keydown = (event) => {
        if (sendable(event.code)) {
            event.preventDefault();
            if (!pressed.has(event.code)) {
                pressed.add(event.code);
                send(event.code, true);
            }
        }
    };
    const keyup = (event) => {
        if (pressed.delete(event.code)) {
            event.preventDefault();
            send(event.code, false);
        }
    };
    function sendable(code) { return map.has(code); }
    const releaseAll = () => {
        for (const code of pressed) send(code, false);
        pressed.clear();
    };
    const onBlur = () => releaseAll();
    canvas.addEventListener("keydown", keydown);
    window.addEventListener("keyup", keyup);
    window.addEventListener("blur", onBlur);
    displays.set(canvas, { context, imageData, callback, keydown, keyup, onBlur, releaseAll });
}

export function render(canvas, rgba) {
    const display = displays.get(canvas);
    if (!display) return;
    if (rgba.length !== display.imageData.data.length) throw new Error("Invalid frame buffer size");
    display.imageData.data.set(rgba);
    display.context.putImageData(display.imageData, 0, 0);
}

export function focus(canvas) { canvas.focus(); }

export function dispose(canvas) {
    const display = displays.get(canvas);
    if (!display) return;
    display.releaseAll();
    canvas.removeEventListener("keydown", display.keydown);
    window.removeEventListener("keyup", display.keyup);
    window.removeEventListener("blur", display.onBlur);
    displays.delete(canvas);
}
