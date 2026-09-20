import { afterRenderEffect, Component, ElementRef, input, signal, viewChild } from '@angular/core';

@Component({
  selector: 'app-overflow-row',
  template: `
    <div
      class="overflow-row"
      [class.on-orange]="tone() === 'on-orange'"
      [class.on-light]="tone() === 'on-light'"
      [class.has-overflow-start]="canScrollStart()"
      [class.has-overflow-end]="canScrollEnd()">
      @if (canScrollStart()) {
        <button
          type="button"
          class="overflow-arrow start"
          (click)="scroll(-1)"
          [attr.aria-label]="previousLabel()">
          <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M15.4 4.6 8 12l7.4 7.4 1.2-1.2L10.4 12l6.2-6.2z"/>
          </svg>
        </button>
      }
      <div
        #track
        class="overflow-track"
        (scroll)="updateScroll()"
        (wheel)="onWheel($event)">
        <ng-content />
      </div>
      @if (canScrollEnd()) {
        <button
          type="button"
          class="overflow-arrow end"
          (click)="scroll(1)"
          [attr.aria-label]="nextLabel()">
          <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="m8.6 4.6-1.2 1.2L13.6 12l-6.2 6.2 1.2 1.2L16 12z"/>
          </svg>
        </button>
      }
    </div>
  `,
  styleUrl: './overflow-row.scss',
  host: {
    '(window:resize)': 'updateScroll()',
  },
})
export class OverflowRowComponent {
  readonly tone = input<'on-orange' | 'on-light'>('on-light');
  readonly previousLabel = input('Show previous');
  readonly nextLabel = input('Show more');
  readonly contentKey = input(0);

  private readonly track = viewChild<ElementRef<HTMLElement>>('track');
  protected readonly canScrollStart = signal(false);
  protected readonly canScrollEnd = signal(false);

  constructor() {
    afterRenderEffect(() => {
      this.contentKey();
      this.tone();
      this.updateScroll();
    });
  }

  protected updateScroll() {
    const el = this.track()?.nativeElement;
    if (!el) {
      this.canScrollStart.set(false);
      this.canScrollEnd.set(false);
      return;
    }

    const maxScroll = el.scrollWidth - el.clientWidth;
    this.canScrollStart.set(el.scrollLeft > 4);
    this.canScrollEnd.set(maxScroll - el.scrollLeft > 4);
  }

  protected scroll(direction: -1 | 1) {
    const el = this.track()?.nativeElement;
    if (!el) {
      return;
    }

    const distance = Math.max(el.clientWidth * 0.7, 180);
    el.scrollBy({ left: direction * distance, behavior: 'smooth' });
  }

  protected onWheel(event: WheelEvent) {
    const el = this.track()?.nativeElement;
    if (!el || (!this.canScrollStart() && !this.canScrollEnd())) {
      return;
    }

    if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) {
      return;
    }

    event.preventDefault();
    el.scrollLeft += event.deltaY;
    this.updateScroll();
  }
}
