# Standard Ant Design UI alignment

Stage11.14 update: standard geometry remains; theme tokens now use darker official
palette shades and readable secondary text for AA contrast. Exact colors below
describe the earlier baseline. See [frontend acceptance](frontend-acceptance.md).

2026-10-20. Explicit UI maintenance after11.9; no later feature stage started.
The new request replaces the previous custom teal theme with the installed **Ant Design
6.6.6 default light theme**. React19, TypeScript, Vite and business/API behavior remain.

## Audit

- ConfigProvider, Ant App context and `antd/dist/reset.css` were already correct.
  No Bootstrap, Tailwind, third-party reset or competing CSS framework was installed.
- `theme.ts` changed primary/info/state colors, surfaces, text/borders, fonts, headings,
  controlHeight36, radius8 and shadows. Overrides also changed Layout/Menu/Button/
  Typography/Card/Statistic. This was the main source of visual divergence.
- App CSS repeated flex/grid layouts, added a custom brand badge/metric border, wrapped
  email at arbitrary characters and globally forced animations with `!important`.
- Layout/Menu/Table/Pagination/Form/Modal were already real Ant components. No legacy
  ReactDOM or deprecated column renderer patterns needed replacement. Error composition
  could use Result. Invite's custom body footer could use the standard Modal footer.

## Changes

`theme.ts` retains only a stable cssVar scope, with no token/component/algorithm overrides.
Ant defaults supply blue, state colors, typography, radii, shadows and control geometry.
No dark/compact algorithm, global componentSize or custom palette. The light layout header
uses the default colorBgContainer locally, without changing component tokens.

Layout retains240px Sider/80px collapsed and mobile Drawer, with64px Header,24px page
padding/16px mobile and90rem content maximum. Flex/Space compose the header/PageHeader;
Typography.Title/Text and standard buttons provide hierarchy. Menu owns selected/hover/
icons. Brand is an Ant icon plus Typography/Space, without a colored badge/custom font.

Users filters use vertical Form/Row/Col and controlled Input/Select. No duplicate Form
state or changes to URL/debounce/server queries. Table uses default size/hover/header/
sort controls, ellipsis for email/name and adequate nowrap Details width. Full email is
available in the native cell title/details. The focusable horizontal wrapper remains.
Pagination and page-size Select remain standard Ant components outside that wrapper to
preserve mobile keyboard access, custom current sizes and existing int32 offset limits.
Ant Empty remains outside the wide table for mobile visibility, with normal-sized text.
Errors use Ant Result and retain safe messages/reference/manual GET retry.

Invite uses default Modal520/title/body/footer/buttons and vertical Form. The footer
submits the form via generated React useId/native form association, retaining Enter,
validation, pending, CSRF,202/409, roles, clearing and focus return. No request on open/close.
Dashboard uses Row/Col/Card/Statistic; Recharts colors/tooltip shadow use default tokens.
Accessible daily-value fallback stays semantic HTML, with local table markup CSS only.

CSS now handles page geometry, overflow, chart fallback and accessibility. No fixed colors,
Ant internal class overrides, `!important` or broad animation suppression. The scoped
keyboard ring is for plain links/summary/scroll regions; Ant controls own their styling.
No API/schema/migrations/dependencies/lockfile/config sample or security policy changes.

## Official references and verification

Reviewed the current official6.6.6 pages and Basic Modal rendered in the browser:
[overview](https://ant.design/components/overview/),
[Layout](https://ant.design/components/layout/),
[Modal](https://ant.design/components/modal/),
[Table](https://ant.design/components/table/),
[theme defaults](https://ant.design/docs/react/customize-theme/).
Only the text request was attached; no reference image was present. The implementation
uses default library components, not CSS copied from the documentation site.

`e2e/antDesign.spec.ts` adds three real-backend visual scenarios at360/768/1280, capturing
Dashboard, Users and Invite. Assertions check actual default blue,32px controls,6px button
radius, ellipsis, footer association, focus return and no page overflow. Its project runs
after existing account lifecycle scenarios and uses only their disposable fixture data.

```powershell
# From repository root: real HTTPS API/Vite, PostgreSQL17/Mailpit, API drift/build/Chromium
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
```

PNGs are in ignored `test-results/antDesign-*/` directories: `dashboard.png`, `users.png`,
`invite-user.png`. Capture disables animations only for the screenshot, showing the
completed dialog instead of its enter frame; production motion is unchanged. Existing
keyboard/lifecycle tests run normally. Images contain only disposable data/blank form,
no passwords/tokens. Manual review compares composition to official examples, not pixels.

Commands/results and initial corrected failures are in IMPLEMENTATION_STATUS.md.
Firefox/WebKit/axe/Lighthouse/production hosting remain untested. Future stages11.10–11.14/
12 and existing MFA/restricted-network requirements remain outside this UI request.
