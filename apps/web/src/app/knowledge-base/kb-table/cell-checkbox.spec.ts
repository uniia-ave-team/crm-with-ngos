import { CHECKBOX_OFF, CHECKBOX_ON, checkboxLosesText, checkboxState, setCheckbox, toCheckbox, toggleCheckboxFormat } from './cell-checkbox';

describe('cell check box', () => {
  const coloured = (html: string) => `<div style="background-color: #fde2e4">${html}</div>`;

  it('is recognised with its formatting and background colour, and switched keeping them', () => {
    expect(checkboxState(CHECKBOX_ON)).toBe(true);
    expect(checkboxState(coloured(`<b>${CHECKBOX_OFF}</b>`))).toBe(false);
    expect(checkboxState('Фонд ☑')).toBeNull();
    expect(setCheckbox(coloured(`<b>${CHECKBOX_OFF}</b>`), true)).toBe(coloured(`<b>${CHECKBOX_ON}</b>`));
  });

  it('is formatted as a whole: a tag around the symbol, on and off', () => {
    const bold = toggleCheckboxFormat(coloured(CHECKBOX_ON), 'bold');
    expect(bold).toBe(coloured(`<b>${CHECKBOX_ON}</b>`));
    expect(toggleCheckboxFormat(bold, 'bold')).toBe(coloured(CHECKBOX_ON));
  });

  it('takes the place of «yes»-like text ticked and of the rest empty, telling when text would go', () => {
    expect(toCheckbox('Так')).toBe(CHECKBOX_ON);
    expect(toCheckbox(coloured('ні'))).toBe(coloured(CHECKBOX_OFF));
    expect(toCheckbox(null)).toBe(CHECKBOX_OFF);
    expect(checkboxLosesText('+')).toBe(false);
    expect(checkboxLosesText('')).toBe(false);
    expect(checkboxLosesText('Фонд')).toBe(true);
  });
});
