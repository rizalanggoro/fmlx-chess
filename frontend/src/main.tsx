import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { Toaster } from './components/ui/toast'
import { client } from './lib/api'
import { connection, rejoinGroups, startHub } from './lib/game-hub'
import './index.css'
import { routeTree } from './routeTree.gen'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5004'

client.setConfig({ baseUrl: apiUrl })

const queryClient = new QueryClient({
  mutationCache: new MutationCache({
    onSuccess: () => {
      void queryClient.invalidateQueries()
    },
  }),
})

connection.on('GameChanged', () => void queryClient.invalidateQueries())
connection.onreconnected(() => {
  void rejoinGroups().then(() => queryClient.invalidateQueries())
})
void startHub().catch(() => {
  console.warn('Hub realtime tidak terhubung. Pastikan backend jalan.')
})

const router = createRouter({ routeTree })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  </StrictMode>,
)
