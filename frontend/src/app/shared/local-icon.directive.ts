import { AfterContentInit, Directive, ElementRef, Injectable, inject } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { MatIcon, MatIconRegistry } from '@angular/material/icon';

const iconNamePattern = /^[a-z0-9_]+$/;

/** Registers the locally bundled SVG icon set with Angular Material. */
@Injectable({ providedIn: 'root' })
export class LocalIconRegistry {
  private readonly registry = inject(MatIconRegistry);
  private readonly sanitizer = inject(DomSanitizer);

  constructor() {
    this.registry.addSvgIconResolver((name, namespace) => {
      if (namespace || !iconNamePattern.test(name)) return null;
      return this.sanitizer.bypassSecurityTrustResourceUrl(`/icons/${name}.svg`);
    });
  }
}

/**
 * Converts existing ligature-based <mat-icon> markup to a root-published local SVG.
 * Keeping the element and its classes intact means existing page styles continue to work.
 */
@Directive({
  selector: 'mat-icon:not([svgIcon]):not([fontIcon])',
})
export class LocalIconDirective implements AfterContentInit {
  private readonly element = inject(ElementRef<HTMLElement>);
  private readonly matIcon = inject(MatIcon);
  private readonly iconRegistry = inject(LocalIconRegistry);

  ngAfterContentInit(): void {
    // Interpolated icon names have been written by this point. Defer once so nested views
    // and Angular Material have completed their initial content setup.
    queueMicrotask(() => this.useLocalSvg());
  }

  private useLocalSvg(): void {
    void this.iconRegistry;
    const name = this.element.nativeElement.textContent?.trim() ?? '';
    if (iconNamePattern.test(name)) this.matIcon.svgIcon = name;
  }
}
