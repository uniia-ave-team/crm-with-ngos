import { joinBackground, richTextToPlain, sanitizeRichText, splitBackground } from './rich-text';

describe('rich text', () => {
  it('keeps plain text as it is', () => {
    expect(sanitizeRichText('Просто текст')).toBe('Просто текст');
  });

  it('keeps only bold, italic, underline, strikethrough and line breaks, under one name each', () => {
    expect(sanitizeRichText('<strong>а</strong> <em>б</em> <ins>в</ins> <strike>г</strike><br><del>д</del>')).toBe(
      '<b>а</b> <i>б</i> <u>в</u> <s>г</s><br><s>д</s>',
    );
  });

  it('drops every other tag, attribute and style, keeping the text, and escapes it', () => {
    expect(sanitizeRichText('<span style="color:red" onclick="x()">а</span><script>alert(1)</script><b class="x">б &lt;в&gt;</b>')).toBe(
      'аalert(1)<b>б &lt;в&gt;</b>',
    );
  });

  it('turns the lines of the browser editor into line breaks and drops the trailing one', () => {
    expect(sanitizeRichText('перший<div>другий</div><div><br></div>')).toBe('перший<br>другий');
    expect(sanitizeRichText('рядок<br>')).toBe('рядок');
  });

  it('gives the plain text for copying', () => {
    expect(richTextToPlain('<b>Фонд</b> «Розвиток»<br>друга &amp; третя')).toBe('Фонд «Розвиток»\nдруга & третя');
  });

  it('keeps the background of the whole cell as an invisible mark at the start of the value', () => {
    expect(joinBackground('#FDE2E4', '<b>Фонд</b>')).toBe('<div style="background-color: #fde2e4"><b>Фонд</b></div>');
    expect(splitBackground('<div style="background-color: #fde2e4"><b>Фонд</b></div>')).toEqual({ background: '#fde2e4', html: '<b>Фонд</b>' });
    // Стара позначка теж читається.
    expect(splitBackground('<!--bg:#fde2e4--><b>Фонд</b>')).toEqual({ background: '#fde2e4', html: '<b>Фонд</b>' });
    expect(splitBackground('Фонд')).toEqual({ background: null, html: 'Фонд' });
    expect(joinBackground(null, 'Фонд')).toBe('Фонд');
    expect(richTextToPlain('<div style="background-color: #fde2e4"><b>Фонд</b></div>')).toBe('Фонд');
  });
});
