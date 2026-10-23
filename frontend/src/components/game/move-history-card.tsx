import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { FILES } from '@/constants/chess'
import type { MoveHistoryEntry, Position } from '@/lib/api'

const alg = (p: Position) => `${FILES[p.column]}${p.row + 1}`

const fmtMove = (h: MoveHistoryEntry) =>
  `${alg(h.move.from)}→${alg(h.move.to)}${h.move.promotionType ? `=${h.move.promotionType[0]}` : ''}`

export function MoveHistoryCard({ history }: { history: MoveHistoryEntry[] }) {
  const rounds = history.reduce<MoveHistoryEntry[][]>((acc, move, i) => {
    if (i % 2 === 0) acc.push([move])
    else acc[acc.length - 1].push(move)
    return acc
  }, [])

  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle className="font-heading text-xl">Riwayat</CardTitle>
        <CardAction>
          <p className="text-xs text-muted-foreground">{history.length} langkah</p>
        </CardAction>
      </CardHeader>
      <CardContent>
        {history.length === 0 ? (
          <p className="text-sm text-muted-foreground">Belum ada langkah.</p>
        ) : (
          <ScrollArea className="max-h-64">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="h-8! w-8 px-1 text-xs" />
                  <TableHead className="h-8! px-1 text-xs">Putih</TableHead>
                  <TableHead className="h-8! px-1 text-xs">Hitam</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody className="font-mono">
                {rounds.map((round, i) => (
                  <TableRow key={i}>
                    <TableCell className="p-1! text-muted-foreground">{i + 1}.</TableCell>
                    {round.map((h, j) => (
                      <TableCell key={j} className="p-1!">
                        {fmtMove(h)}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  )
}
