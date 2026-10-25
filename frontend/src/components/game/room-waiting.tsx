import { Eyebrow, StateScreen } from '@/components/room-chrome'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

export function RoomWaiting({
  roomId,
  playerLabel,
  onBack,
}: {
  roomId: string
  playerLabel?: string
  onBack: () => void
}) {
  return (
    <StateScreen>
      <Card className="w-full text-center">
        <CardHeader>
          <Eyebrow>Online</Eyebrow>
          <CardTitle className="text-2xl">Menunggu lawan bergabung…</CardTitle>
          <CardDescription>Bagikan kode room ini ke lawan.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="font-mono text-3xl font-bold tracking-[0.25em] text-foreground">
            {roomId}
          </p>
          <Button variant="outline" onClick={onBack}>
            Lobby
          </Button>
          {playerLabel && (
            <p className="text-sm text-muted-foreground">
              Kamu main sebagai{' '}
              <span className="font-semibold text-foreground">{playerLabel}</span>
            </p>
          )}
        </CardContent>
      </Card>
    </StateScreen>
  )
}
