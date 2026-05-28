# VisionEdit Studio Brand Spec

Derived from `mobile-prototype-2.html` and user redesign requests.

## Color Tokens (OKLch)
- `--bg`:      oklch(12% 0.02 260)  /* Deepest navy workspace */
- `--surface`: oklch(18% 0.03 260)  /* Glassmorphic panels */
- `--fg`:      oklch(98% 0.01 260)  /* Crisp primary text */
- `--muted`:   oklch(65% 0.02 260)  /* Secondary metadata */
- `--border`:  rgba(255, 255, 255, 0.08) /* Hairline dividers */
- `--accent`:  oklch(75% 0.16 195)  /* Electric Cyan action color */

## Typography
- **Display**: -apple-system, BlinkMacSystemFont, 'SF Pro Display', system-ui, sans-serif
- **Body**:    -apple-system, BlinkMacSystemFont, 'SF Pro Text', system-ui, sans-serif
- **Mono**:    'JetBrains Mono', 'SF Mono', ui-monospace, monospace

## Layout Posture
- **Navigation**: Floating pill-shaped bar at the bottom with 24px-32px offset.
- **Containers**: Generous radii (24px-32px for sheets, 16px for cards).
- **Surfaces**: Heavy use of `backdrop-filter: blur(24px)` and `saturate(180%)`.
- **Borders**: 1px solid hairlines; no heavy drop shadows except for elevation depth.
- **Accents**: Restricted to one primary action (CTA) and active navigation states.
