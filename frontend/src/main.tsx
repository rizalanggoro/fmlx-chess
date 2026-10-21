import {StrictMode} from 'react'
import {createRoot} from 'react-dom/client'
import {MutationCache, type Query, QueryClient, QueryClientProvider} from '@tanstack/react-query'
import {createRouter, RouterProvider} from '@tanstack/react-router'
import {Toaster} from './components/ui/toast'
import {client} from './lib/api'
import {connection, joinGroup, LOBBY_GROUP, rejoinGroups, startHub} from './lib/game-hub'
import './index.css'
import {routeTree} from './routeTree.gen'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5004'

client.setConfig({baseUrl: apiUrl})

const queryClient = new QueryClient({
  mutationCache: new MutationCache({
    onSuccess: () => {
      void invalidateGameQueries()
    },
  }),
})

const isBoardQuery = (query: Query) => {
  const key = query.queryKey[0]
  return typeof key === 'object' && key !== null && '_id' in key &&
    key._id === 'getRoomsByRoomIdGameBoard'
}

const invalidateGameQueries = () =>
  queryClient.invalidateQueries({predicate: (query) => !isBoardQuery(query)})

connection.on('GameChanged', () => void invalidateGameQueries())
connection.onreconnected(() => {
  void rejoinGroups().then(() => queryClient.invalidateQueries())
})
void startHub()
  .then(() => joinGroup(LOBBY_GROUP))
  .catch(() => {
    console.warn('Hub realtime tidak terhubung. Pastikan backend jalan.')
  })

const router = createRouter({routeTree})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router}/>
      <Toaster/>
    </QueryClientProvider>
  </StrictMode>,
)
