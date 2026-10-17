# Third-party notices

Trail ships or uses the following third-party software. Each vendored library keeps its
license text next to its files. Versions and SHA-256 checksums are pinned in
[`eng/frontend.props`](eng/frontend.props), and the vendored files are refreshed with
`dotnet msbuild src/Trail.Web/Trail.Web.csproj -t:UpdateFrontendLibraries`.

| Component | Version | License | Source | Files in this repository |
|---|---|---|---|---|
| daisyUI | 5.7.50 | MIT | https://github.com/saadeghi/daisyui | `src/Trail.Web/Styles/daisyui.mjs`, `src/Trail.Web/Styles/daisyui-theme.mjs` (license in `src/Trail.Web/Styles/daisyui.LICENSE`) |
| htmx | 4.0.0 | 0BSD | https://github.com/bigskysoftware/htmx | `src/Trail.Web/wwwroot/lib/htmx/` |
| Chart.js | 4.5.1 | MIT | https://github.com/chartjs/Chart.js | `src/Trail.Web/wwwroot/lib/chart.js/` |
| Lucide | 1.58.0 | ISC (icons derived from Feather: MIT) | https://github.com/lucide-icons/lucide | `src/Trail.Web/wwwroot/lib/lucide/` (sprite with the icons listed in `eng/frontend.props`) |
| Tailwind CSS | 4.3.3 | MIT | https://github.com/tailwindlabs/tailwindcss | Not committed: the standalone executable is downloaded to `.tools/` during the build, and the generated `app.css` keeps its license banner |
