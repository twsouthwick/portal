# TypeScript Helper Functions

This directory contains TypeScript modules that are compiled into `wwwroot/dist/app.js` for use in Blazor components.

## Exported Functions

The compiled `app.js` module exports three helper functions that are consumed via the `IClientBrowserSession` service:

### 1. `isPrivateMode(): Promise<boolean>`

Detects if the browser is currently in private/incognito mode using the `detectincognitojs` library.

**Returns:** `Promise<boolean>` - `true` if in private mode, `false` otherwise

**Use Case:** Filter out private sites when the user is not browsing in incognito mode.

---

### 2. `launchAppUrl(url: string): Promise<boolean>`

Attempts to launch an app protocol URL (e.g., `audiobookshelf://`, `plappa://`) and detects if it succeeded by monitoring window focus events.

**Parameters:**
- `url: string` - The app protocol URL to launch

**Returns:** `Promise<boolean>` - `true` if the app likely launched (window lost focus), `false` otherwise

**Use Case:** Launch native mobile/desktop apps with fallback to app store links if the app isn't installed.

---

### 3. `openUrlInNewTab(url: string): void`

Opens a URL in a new browser tab.

**Parameters:**
- `url: string` - The URL to open

**Returns:** `void`

**Use Case:** Open web URLs in new tabs from Blazor components.

---

## Usage in Blazor

Instead of calling JavaScript directly, use the `IClientBrowserSession` service:

```csharp
@inject IClientBrowserSession BrowserSession

// Check if in private mode
var isPrivate = await BrowserSession.IsPrivateModeAsync();

// Launch an app protocol URL
var launched = await BrowserSession.LaunchAppUrlAsync("myapp://launch");

// Open URL in new tab
await BrowserSession.OpenUrlInNewTabAsync("https://example.com");
```

The service abstracts the JavaScript interop and provides a clean, testable interface.

---

## Building

TypeScript 7 performs type checking, while webpack and esbuild transpile and bundle the browser assets.

```bash
# Install dependencies (first time)
npm install

# Production build (minified)
npm run build

# Development build (with source maps)
npm run dev

# Watch mode for development
npm run watch
```

## File Structure

```
ts/
? app.ts                      # Main entry point - imports CSS and exports all functions
? applaunch.ts                # URL launching helpers
? browserdetect.ts            # Browser detection helpers
? blazor-reconnect.ts         # Blazor reconnect modal customization

css/
? app.css                     # Main stylesheet (imports bootstrap-icons)
```

## Webpack Output

All files are compiled to `wwwroot/dist/`:
- `app.js` - Bundled JavaScript module (ES module format)
- `app.css` - Bundled CSS including bootstrap-icons
- `*.woff`, `*.woff2` - Bootstrap icon fonts

## Service Implementation

The `IClientBrowserSession` service is implemented by `JsClientBrowserSession` which:
- Lazily loads the JavaScript module on first use
- Caches the module reference for the lifetime of the service (scoped)
- Handles JavaScript exceptions gracefully with fallback behavior
- Implements `IAsyncDisposable` to properly clean up the module reference

## Dependencies

- **detectincognitojs** - Library for detecting private/incognito mode across different browsers
- **bootstrap-icons** - Icon font library used throughout the app
