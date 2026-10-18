import { defineConfig } from '@hey-api/openapi-ts'

export default defineConfig({
  input: 'openapi.json',
  output: {
    path: 'src/lib/api',
  },
  plugins: [
    '@hey-api/typescript',
    '@hey-api/sdk',
    { name: '@hey-api/client-fetch', exportFromIndex: true },
    { name: '@tanstack/react-query', exportFromIndex: true },
  ],
})
