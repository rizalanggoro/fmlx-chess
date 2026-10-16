import { Button } from '@/components/ui/button'
import { useStartGame } from '@/lib/chess'
import { createFileRoute, useNavigate } from '@tanstack/react-router'

export const Route = createFileRoute('/')({ component: Lobby })

function Lobby() {
  const navigate = useNavigate()
  const start = useStartGame()

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-8 p-4">
      <h1 className="text-4xl font-bold">fmlx-chess</h1>
      <div className="flex gap-4">
        <Button
          size="lg"
          disabled={start.isPending}
          onClick={() =>
            start.mutate(undefined, { onSuccess: () => void navigate({ to: '/game' }) })
          }
        >
          Main Hotseat
        </Button>
        <Button size="lg" variant="outline" disabled title="Segera hadir">
          Main Online
        </Button>
      </div>
      {start.isError && <p className="text-sm text-red-500">Gagal memulai game. Pastikan backend jalan.</p>}
    </div>
  )
}
