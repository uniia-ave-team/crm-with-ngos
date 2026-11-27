import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { RichTextDirective, formatSelection } from './rich-text.directive';

@Component({
  imports: [RichTextDirective],
  template: `<div contenteditable="true" [appRichText]="value()" (richTextChange)="changes.push($event)"></div>`,
})
class HostComponent {
  readonly value = signal('<b>Фонд</b>');
  readonly changes: string[] = [];
}

describe('RichTextDirective', () => {
  afterEach(() => vi.restoreAllMocks());

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return { fixture, host: fixture.componentInstance, block: (fixture.nativeElement as HTMLElement).querySelector('div')! };
  }

  it('shows the value and gives every change already cleaned', () => {
    const { host, block } = render();
    expect(block.innerHTML).toBe('<b>Фонд</b>');
    block.innerHTML = '<strong>Фонд</strong> <span style="color:red">новий</span>';
    block.dispatchEvent(new Event('input'));
    expect(host.changes).toEqual(['<b>Фонд</b> новий']);
  });

  it('cleans a value from outside before it gets into the block: no script, no element or attribute besides the formatting', () => {
    const { fixture, host, block } = render();
    const hostile = vi.fn();
    (window as unknown as Record<string, unknown>)['hostileValue'] = hostile;
    host.value.set('<img src="x" onerror="hostileValue()"><b style="position:fixed;inset:0">Фонд</b><script>hostileValue()</script><a href="javascript:hostileValue()">посилання</a>');
    fixture.detectChanges();
    expect(block.innerHTML).toBe('<b>Фонд</b>посилання');
    expect(block.querySelector('img, script, a, [style], [onerror]')).toBeNull();
    expect(hostile).not.toHaveBeenCalled();
    delete (window as unknown as Record<string, unknown>)['hostileValue'];
  });

  it('writes a value from outside only when it is really different (the cursor does not jump while typing)', () => {
    const { fixture, host, block } = render();
    const text = block.firstChild;
    host.value.set('<b>Фонд</b>');
    fixture.detectChanges();
    expect(block.firstChild).toBe(text);
    host.value.set('');
    fixture.detectChanges();
    expect(block.innerHTML).toBe('');
  });

  it('makes Enter a line break and pastes plain text', () => {
    const exec = vi.fn().mockReturnValue(true);
    Object.defineProperty(document, 'execCommand', { value: exec, configurable: true });
    const { block } = render();
    const enter = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true });
    block.dispatchEvent(enter);
    expect(enter.defaultPrevented).toBe(true);
    expect(exec).toHaveBeenCalledWith('insertLineBreak');

    const paste = new Event('paste', { cancelable: true }) as ClipboardEvent;
    Object.defineProperty(paste, 'clipboardData', { value: { getData: () => 'простий текст' } });
    block.dispatchEvent(paste);
    expect(paste.defaultPrevented).toBe(true);
    expect(exec).toHaveBeenCalledWith('insertText', false, 'простий текст');
  });

  it('formats the selection with the commands of the browser, without CSS styles', () => {
    const exec = vi.fn().mockReturnValue(true);
    Object.defineProperty(document, 'execCommand', { value: exec, configurable: true });
    formatSelection('underline');
    expect(exec).toHaveBeenNthCalledWith(1, 'styleWithCSS', false, 'false');
    expect(exec).toHaveBeenNthCalledWith(2, 'underline');
  });
});
