# DocLens

Generate a **beautiful, self-contained HTML API reference** for a .NET library
directly from its assembly and XML documentation file.

No running app. No HTTP host. No Swagger. DocLens reads a `.dll` plus its
compiler-generated `.xml` doc file and produces a single static HTML page you
can open from disk, commit to a repo, or publish to any static host.

## Why DocLens

Swagger/OpenAPI documents HTTP endpoints of a *running* web app. That does not
help when you ship a **class library** (a NuGet package, an internal SDK, a
WinForms/WPF helper). For libraries the usual option is DocFX, which is heavy,
slow, and produces a multi-file site that needs a build pipeline.

DocLens targets the gap:

- **Class libraries, not HTTP.** Documents public/protected types and members.
- **Static, no runtime.** The assembly is inspected with
  `MetadataLoadContext` (no code from the target assembly is executed).
- **One file.** CSS and JavaScript are inlined; the output has zero external
  dependencies.
- **Fast.** A typical library renders in well under a second.

## How it works

```
assembly.dll  ─┐
               ├─►  ApiExtractor (MetadataLoadContext + reflection)
assembly.xml  ─┘            │
                            ▼
                       ApiDocument (model)
                            │
                            ▼
                       HtmlRenderer  ─►  index.html  (self-contained)
```

1. **DocIdGenerator** builds ECMA-334 XML documentation comment IDs from
   reflection (e.g. `M:MyLib.Repository`1.Add(`0)`), so they match exactly what
   the compiler wrote into the `.xml` file.
2. **XmlDocParser** parses the `.xml` file, flattening inline tags
   (`<see>`, `<paramref>`, `<c>`) and de-indenting `<code>` blocks.
3. **ApiExtractor** walks the assembly, classifies types, builds C#-style
   signatures, and binds each member to its documentation.
4. **HtmlRenderer** emits a single HTML file with inlined assets.

## Install

As a .NET tool:

```
dotnet tool install -g DocLens.Tool
```

Or reference the library directly:

```
dotnet add package DocLens.Core
```

## Usage (CLI)

```
doclens <assembly> [options]
```

| Option | Description |
|--------|-------------|
| `-o, --output <file>` | Output HTML path (default: `<assembly>.html`). |
| `-x, --xml <file>` | XML doc file (default: `<assembly>.xml` next to the dll). |
| `-t, --title <text>` | Page title (default: `<AssemblyName> API`). |
| `-r, --readme <file>` | Markdown/text shown on the overview page. |
| `--public-only` | Exclude protected members. |
| `-h, --help` | Show help. |

Example:

```
doclens bin/Release/netstandard2.0/MyLib.dll -o docs/index.html -t "MyLib" -r README.md
```

Make sure XML documentation is enabled in your library so the `.xml` file
exists next to the `.dll`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

## Usage (library)

```csharp
using DocLens.Core.Reflection;
using DocLens.Core.Rendering;

var extractor = new ApiExtractor(new ExtractionOptions
{
    Title = "MyLib",
    Description = File.ReadAllText("README.md"),
    IncludeProtected = true,
});

ApiDocument model = extractor.Extract("bin/Release/netstandard2.0/MyLib.dll");
new HtmlRenderer().RenderToFile(model, "docs/index.html");
```

## Output features

The generated page is designed for both readers and developers:

- **Light/dark theme** with system-preference detection and persistence.
- **Instant client-side search** over type names, summaries, and member names
  (press `/`), so searching a method surfaces the type that declares it.
- **Cross-links**: type names in signatures and code link to their own pages.
- **Markdown overview**: the `--readme` file is rendered as Markdown (headings,
  lists, tables, code fences, links, emphasis).
- **Keyboard and screen-reader friendly**: skip link, ARIA labels, visible
  focus outlines, semantic landmarks (`<nav>`, `<main>`).
- **Responsive**: collapsible sidebar on small screens.
- **Copy buttons** on example code blocks.
- **Grouped members** (constructors, properties, methods, events, fields) with
  signatures, parameters, returns, exceptions, remarks, and examples.

## Dependency resolution

DocLens automatically discovers the installed shared frameworks
(`Microsoft.NETCore.App`, `Microsoft.WindowsDesktop.App`, etc.), so it can
document assemblies that depend on WinForms/WPF or ASP.NET. For third-party
dependencies in non-standard locations, add directories via
`ExtractionOptions.AdditionalSearchDirectories`.

## Supported API surface

Classes, structs, interfaces, enums, delegates; constructors, methods
(including generic methods and operators), properties, indexers, events, fields,
and constants; nested types; generic type parameters; `ref`/`out`/`in`/`params`
modifiers; nullable and array types; C# keyword aliases (`int`, `string`, …).

## Project layout

```
DocLens/
├── src/
│   ├── DocLens.Core/        netstandard2.0 library (extractor + renderer)
│   │   ├── Model/           ApiDocument, ApiType, ApiMember, XmlDocEntry
│   │   ├── Reflection/      DocIdGenerator, ApiExtractor, SignatureBuilder, TypeNameFormatter
│   │   ├── Xml/             XmlDocParser
│   │   └── Rendering/       HtmlRenderer + embedded CSS/JS/HTML assets
│   └── DocLens.Tool/        net8.0 dotnet tool (`doclens`)
└── tests/
    ├── DocLens.SampleLib/   richly documented fixture library
    └── DocLens.Tests/       unit + dogfood tests
```

## Testing approach

DocLens is validated by **dogfooding**: the test fixture
(`DocLens.SampleLib`) is compiled with XML docs, and tests assert that every
documentation ID the C# compiler emitted can be reproduced by `DocIdGenerator`
from reflection. The extractor and renderer are then run end-to-end over the
compiled fixture. This catches real metadata edge cases (operators, generic
arity, indexers, `MetadataLoadContext` limitations) that mocks would miss.

```
dotnet build
dotnet test
```

## License

MIT. See [LICENSE](LICENSE).
