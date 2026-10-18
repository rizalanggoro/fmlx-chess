import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { HubConnectionBuilder } from '@microsoft/signalr'
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { client } from './lib/api'
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

const connection = new HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/game`, { withCredentials: false })
  .withAutomaticReconnect()
  .build()

connection.on('GameChanged', () => void queryClient.invalidateQueries())
connection.onreconnected(() => void queryClient.invalidateQueries())
void connection.start().catch(() => {
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
    </QueryClientProvider>
  </StrictMode>,
)
