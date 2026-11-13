// Builds an ASS subtitle file: the voice-over text, split into short lines and timed across each clip in proportion to length.
const ts = (s) => {
  s = Math.max(0, s); const h = Math.floor(s / 3600); const m = Math.floor((s % 3600) / 60); const sec = s % 60;
  return `${h}:${String(m).padStart(2, '0')}:${sec.toFixed(2).padStart(5, '0')}`;
};
const esc = (t) => t.replace(/[{}]/g, '').replace(/\n/g, '\\N');

/** Splits text into caption chunks of at most `max` characters, breaking at sentence ends, then commas, then words. */
export function chunk(text, max = 84) {
  const sentences = text.match(/[^.!?:]+[.!?:]?/g).map((s) => s.trim()).filter(Boolean);
  const out = [];
  for (const s of sentences) {
    if (s.length <= max) { out.push(s); continue; }
    let cur = '';
    for (const part of s.split(/(?<=,)\s+|\s+/)) {
      if ((cur + ' ' + part).trim().length > max) { out.push(cur.trim()); cur = part; } else cur = (cur + ' ' + part).trim();
    }
    if (cur) out.push(cur);
  }
  return out;
}

/**
 * items: [{ start, duration, text }] in seconds. options: width/height of the video, margins, font size.
 * Returns the ASS file text.
 */
export function buildAss(items, { width = 1920, height = 1080, fontSize = 40, marginV = 54, marginL = 120, marginR = 120, align = 2 } = {}) {
  const head = `[Script Info]
ScriptType: v4.00+
PlayResX: ${width}
PlayResY: ${height}
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Cap,Segoe UI Semibold,${fontSize},&H00FFFFFF,&H00FFFFFF,&H38263A44,&H00000000,0,0,0,0,100,100,0,0,3,14,0,${align},${marginL},${marginR},${marginV},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
`;
  const lines = [];
  for (const it of items) {
    const parts = chunk(it.text);
    const total = parts.reduce((a, p) => a + p.length, 0);
    let t = it.start;
    for (const p of parts) {
      const d = (it.duration * p.length) / total;
      lines.push(`Dialogue: 0,${ts(t)},${ts(t + d - 0.05)},Cap,,0,0,0,,{\\fad(120,120)}${esc(p)}`);
      t += d;
    }
  }
  return head + lines.join('\n') + '\n';
}
