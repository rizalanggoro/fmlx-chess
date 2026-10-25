import { Eyebrow, StateScreen } from '@/components/room-chrome'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

export function RoomNotFound({ onBack }: { onBack: () => void }) {
  return (
    <StateScreen>
      <Card className="w-full text-center">
        <CardHeader>
          <Eyebrow>Room</Eyebrow>
          <CardTitle className="text-2xl">Room tidak ditemukan</CardTitle>
          <CardDescription>Cek lagi kodenya, atau buat room baru dari lobby.</CardDescription>
        </CardHeader>
        <CardContent>
          <Button className="w-full" onClick={onBack}>
            Kembali ke Lobby
          </Button>
        </CardContent>
      </Card>
    </StateScreen>
  )
}
