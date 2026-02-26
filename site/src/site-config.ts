import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'Build Governor',
  description: 'Automatic protection against C++ build memory exhaustion on Windows',
  logoBadge: 'BG',
  brandName: 'Build Governor',
  repoUrl: 'https://github.com/mcp-tool-shop-org/build-governor',
  npmUrl: 'https://www.nuget.org/packages/Gov.Protocol',
  footerText: 'MIT Licensed — built by <a href="https://github.com/mcp-tool-shop-org" style="color:var(--color-muted);text-decoration:underline">mcp-tool-shop-org</a>',

  hero: {
    badge: '.NET / Windows',
    headline: 'Build Governor,',
    headlineAccent: 'builds that slow down instead of crashing.',
    description: 'Lightweight governor that sits between your build system and the compiler. Adaptive concurrency based on commit charge — zero config, automatic throttling, actionable OOM diagnostics.',
    primaryCta: { href: '#quick-start', label: 'Get started' },
    secondaryCta: { href: '#features', label: 'Learn more' },
    previews: [
      { label: 'Setup', code: '.\\scripts\\enable-autostart.ps1' },
      { label: 'Build', code: 'cmake --build . --parallel 16' },
      { label: 'Status', code: 'gov status' },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'features',
      title: 'Why Build Governor?',
      subtitle: 'Parallel builds shouldn\u2019t be a gamble.',
      features: [
        { title: 'Zero Config', desc: 'Wrappers auto-start the governor on first build. No daemon setup, no config files, no admin required.' },
        { title: 'Adaptive Throttling', desc: 'Monitors commit charge in real time. Builds slow down gracefully instead of freezing your machine.' },
        { title: 'Actionable Diagnostics', desc: 'Classifies failures as OOM, paging death, or normal errors. Recommends the right -j value.' },
        { title: 'Fail-Safe', desc: 'If the governor is down, wrappers run tools ungoverned. Your build never depends on the governor being healthy.' },
        { title: 'Token Cost Model', desc: 'Assigns costs by operation type: normal compile (1), template-heavy (2\u20134), LTCG link (8\u201312). Matches real memory profiles.' },
        { title: 'NuGet Libraries', desc: 'Gov.Protocol and Gov.Common let you build custom tooling on top of the governor\u2019s memory metrics and OOM classifier.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'quick-start',
      title: 'Quick Start',
      cards: [
        {
          title: 'Automatic (recommended)',
          code: '# One-time setup (no admin required)\ncd build-governor\n.\\scripts\\enable-autostart.ps1\n\n# That\'s it! All builds are now protected\ncmake --build . --parallel 16\nmsbuild /m:16\nninja -j 8',
        },
        {
          title: 'NuGet libraries',
          code: '<!-- Gov.Protocol \u2014 message DTOs only -->\n<PackageReference Include="Gov.Protocol" Version="1.*" />\n\n<!-- Gov.Common \u2014 memory metrics + OOM classifier -->\n<PackageReference Include="Gov.Common" Version="1.*" />',
        },
      ],
    },
    {
      kind: 'data-table',
      id: 'throttle-levels',
      title: 'Throttle Levels',
      subtitle: 'Adaptive response based on system commit charge.',
      columns: ['Commit Ratio', 'Level', 'Behavior'],
      rows: [
        ['< 80%', 'Normal', 'Grant tokens immediately'],
        ['80\u201388%', 'Caution', 'Slower grants, 200 ms delay'],
        ['88\u201392%', 'SoftStop', 'Significant delays, 500 ms'],
        ['> 92%', 'HardStop', 'Refuse new tokens'],
      ],
    },
    {
      kind: 'data-table',
      id: 'token-costs',
      title: 'Token Cost Model',
      subtitle: 'Costs reflect real-world memory profiles.',
      columns: ['Action', 'Tokens', 'Notes'],
      rows: [
        ['Normal compile', '1', 'Baseline'],
        ['Heavy compile (Boost/gRPC)', '2\u20134', 'Template-heavy'],
        ['Compile with /GL', '+3', 'LTCG codegen'],
        ['Link', '4', 'Base link cost'],
        ['Link with /LTCG', '8\u201312', 'Full LTCG'],
      ],
    },
    {
      kind: 'data-table',
      id: 'packages',
      title: 'NuGet Packages',
      columns: ['Package', 'Purpose'],
      rows: [
        ['Gov.Protocol', 'Shared message DTOs for client\u2013service communication over named pipes'],
        ['Gov.Common', 'Windows memory metrics, OOM classification, auto-start client'],
      ],
    },
    {
      kind: 'data-table',
      id: 'cli',
      title: 'CLI Commands',
      columns: ['Command', 'What It Does'],
      rows: [
        ['gov run -- <build cmd>', 'Run a governed build'],
        ['gov status', 'Check governor status and memory metrics'],
        ['gov run --no-start -- <cmd>', 'Run without auto-starting governor'],
      ],
    },
  ],
};
