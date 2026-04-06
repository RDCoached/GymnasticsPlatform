# User Portal Setup

## Installation

```bash
npm install
```

## Optional Dependencies

### React Markdown (for Programme Builder)

To enable full markdown rendering in the Programme Builder, install:

```bash
npm install react-markdown
```

Then uncomment the import and usage in `src/pages/ProgrammeBuilderPage.tsx`:

```typescript
// Uncomment this line:
import ReactMarkdown from 'react-markdown';

// And replace the renderContent function with:
const renderContent = (content: string) => {
  return <ReactMarkdown>{content}</ReactMarkdown>;
};
```

## Development

```bash
npm run dev
```

## Testing

```bash
npm test          # Watch mode
npm run test:ci   # CI mode
npm run test:coverage  # With coverage
```

## Build

```bash
npm run build
```
