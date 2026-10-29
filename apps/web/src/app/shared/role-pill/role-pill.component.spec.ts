import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { RolePillComponent } from './role-pill.component';
import { RolePillModule } from './role-pill.module';
import { roleColor } from './role-color';

@Component({
  template: `<app-role-pill [name]="name()" [active]="active()" [icon]="icon()" />`,
  imports: [RolePillModule],
})
class HostComponent {
  readonly name = signal('Куратор');
  readonly active = signal(true);
  readonly icon = signal<string | null>(null);
}

describe('RolePillComponent', () => {
  function render(name = 'Куратор', active = true) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.name.set(name);
    fixture.componentInstance.active.set(active);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, host: fixture.componentInstance, element, pill: () => element.querySelector('.pill') as HTMLElement };
  }

  it('shows the name of the role', () => {
    expect(render('Голова ГО').pill().textContent?.trim()).toBe('Голова ГО');
  });

  it('takes both colors from the name: the base and the light one (for the dark theme)', () => {
    const { pill } = render('Куратор');
    const expected = roleColor('Куратор')!;
    expect(pill().style.getPropertyValue('--pill-base')).toBe(expected.base);
    expect(pill().style.getPropertyValue('--pill-light')).toBe(expected.light);
  });

  it('gives different roles different colors, the same role the same one', () => {
    const base = (name: string) => render(name).pill().style.getPropertyValue('--pill-base');
    expect(base('Куратор')).toBe(base('Куратор'));
    expect(base('Куратор')).not.toBe(base('Голова ГО'));
  });

  it('is active (the fill) by default and can be inactive (the border)', () => {
    expect(render('Куратор').pill().classList).toContain('pill--active');
    expect(render('Куратор', false).pill().classList).not.toContain('pill--active');
  });

  it('follows the name and the state when they change', () => {
    const { fixture, host, pill } = render('Куратор', true);
    host.name.set('Член');
    host.active.set(false);
    fixture.detectChanges();
    expect(pill().textContent?.trim()).toBe('Член');
    expect(pill().classList).not.toContain('pill--active');
    expect(pill().style.getPropertyValue('--pill-base')).toBe(roleColor('Член')!.base);
  });

  it('has no colors of its own for a name without letters (the neutral colors of the menu are used)', () => {
    const { pill } = render('123');
    expect(pill().style.getPropertyValue('--pill-base')).toBe('');
    expect(pill().style.getPropertyValue('--pill-light')).toBe('');
    expect(pill().textContent?.trim()).toBe('123');
  });

  it('is created by the module', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    expect(fixture.debugElement.children[0].componentInstance).toBeInstanceOf(RolePillComponent);
  });

  describe('the icon', () => {
    it('is not there by default (the pills of the lists have none)', () => {
      expect(render().element.querySelector('ion-icon')).toBeNull();
    });

    it('is shown before the name when it is given, hidden from the readers, and the text of the pill is still the name', () => {
      const { fixture, host, element, pill } = render('Куратор');
      host.icon.set('ellipse-outline');
      fixture.detectChanges();

      const icon = pill().querySelector('ion-icon') as unknown as HTMLElement & { name: string };
      expect(icon.name).toBe('ellipse-outline');
      expect(icon.getAttribute('aria-hidden')).toBe('true');
      expect(pill().firstElementChild).toBe(icon);
      expect(pill().textContent?.trim()).toBe('Куратор');
      expect(element.querySelector('.pill__name')?.textContent?.trim()).toBe('Куратор');
    });

    it('goes away when it is taken away', () => {
      const { fixture, host, element } = render();
      host.icon.set('ellipse');
      fixture.detectChanges();
      host.icon.set(null);
      fixture.detectChanges();
      expect(element.querySelector('ion-icon')).toBeNull();
    });
  });
});
