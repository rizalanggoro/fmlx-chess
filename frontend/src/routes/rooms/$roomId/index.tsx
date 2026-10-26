import {ChessBoard} from '@/components/chess-board'
import {
  CapturedPiecesDialog,
  ConfirmDialog,
  DrawOfferDialog,
  MoveHistoryCard,
  PlayerTurnCard,
  PromotionDialog,
  RoomLoading,
  RoomNotFound,
  RoomWaiting,
} from '@/components/game'
import {Brand, Shell} from '@/components/room-chrome'
import {
  getRoomsByRoomId,
  getRoomsByRoomIdGameBoardOptions,
  getRoomsByRoomIdGameHistoryOptions,
  getRoomsByRoomIdGameLegalMovesAllOptions,
  getRoomsByRoomIdGameOptions,
  getRoomsByRoomIdOptions,
  postRoomsByRoomIdGameDrawClaimFiftyMutation,
  postRoomsByRoomIdGameDrawClaimThreefoldMutation,
  postRoomsByRoomIdGameDrawOfferMutation,
  postRoomsByRoomIdGameDrawRespondMutation,
  postRoomsByRoomIdGameMovesMutation,
  postRoomsByRoomIdGameResignMutation,
  postRoomsByRoomIdGameStartMutation,
} from '@/lib/api'
import {
  useCheckToast,
  useDrawOffer,
  useGameEnded,
  usePieceCapturedToast,
  useRoomGroup,
  useRoomSnapshot,
} from '@/hooks'
import {colorName, playerLabel, playerName, sameSquare} from '@/constants/chess'
import type {DrawOfferedDto, PieceColor, PieceType, Position} from '@/lib/api'
import {loadRoomSession, type RoomSession} from '@/lib/room-session'
import {useMutation, useQuery} from '@tanstack/react-query'
import {createFileRoute, redirect, useNavigate} from '@tanstack/react-router'
import {useState} from 'react'

export const Route = createFileRoute('/rooms/$roomId/')({
  beforeLoad: async ({params}) => {
    if (loadRoomSession(params.roomId))
      return

    const room = await getRoomsByRoomId({
      path: {
        roomId: params.roomId
      }
    }).catch(() => null)

    if (room?.data?.mode === 'Online')
      throw redirect({to: '/rooms'})
  },
  component: Page,
})

function Page() {
  const {roomId} = Route.useParams()

  const navigate = useNavigate()
  const [selectedSquare, setSelectedSquare] = useState<Position | null>(null)
  const [movingSquare, setMovingSquare] = useState<Position | null>(null)
  const [roomSession] = useState<RoomSession | null>(() => loadRoomSession(roomId))
  const [drawOffer, setDrawOffer] = useState<DrawOfferedDto | null>(null)
  const [pendingPromotion, setPendingPromotion] = useState<{
    from: Position
    to: Position
    options: PieceType[]
  } | null>(null)
  const [capturedDialog, setCapturedDialog] = useState<{
    open: boolean;
    color: PieceColor
  }>({
    open: false,
    color: 'White',
  })

  useRoomGroup(roomId)
  useGameEnded(roomId)
  useRoomSnapshot(roomId)
  useDrawOffer(roomId, roomSession?.color, setDrawOffer)
  usePieceCapturedToast(roomId, roomSession?.color)
  useCheckToast(roomId, roomSession?.color)

  const {
    data: room,
    isError: roomError
  } = useQuery({
    ...getRoomsByRoomIdOptions({path: {roomId}}),
  })
  const {data: state} = useQuery({
    ...getRoomsByRoomIdGameOptions({path: {roomId}}),
  })

  const online = room?.mode === 'Online'
  const waiting = online && !(room?.whiteJoined && room?.blackJoined)
  const myColor = roomSession?.color
  const turn = state?.currentPlayer.color
  const over = state?.state === 'Completed'
  const myTurn = !online || turn === myColor
  const canAct = !online || myColor !== undefined
  const resignColor = online ? myColor : turn
  const activeSquare = myTurn ? selectedSquare : null

  const {
    data: board
  } = useQuery({
    ...getRoomsByRoomIdGameBoardOptions({path: {roomId}}),
  })
  const {
    data: history
  } = useQuery({
    ...getRoomsByRoomIdGameHistoryOptions({path: {roomId}}),
  })
  const {
    data: legal,
    isPending: legalPending
  } = useQuery({
    ...getRoomsByRoomIdGameLegalMovesAllOptions({path: {roomId}}),
    enabled: state?.state === 'InProgress',
  })

  const {
    mutate: makeMove,
    isPending: isMoving
  } = useMutation({
    ...postRoomsByRoomIdGameMovesMutation(),
    onSettled: () => setMovingSquare(null),
  })
  const {
    mutate: start,
    isPending: isStarting
  } = useMutation({
    ...postRoomsByRoomIdGameStartMutation(),
  })
  const {
    mutate: resign,
    isPending: isResigning
  } = useMutation({
    ...postRoomsByRoomIdGameResignMutation(),
  })
  const {
    mutate: offerDraw,
    isPending: isOfferingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawOfferMutation(),
  })
  const {
    mutate: respondDraw,
    isPending: isRespondingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawRespondMutation(),
    onSuccess: () => setDrawOffer(null),
  })
  const {
    mutate: claimThreefold,
    isPending: isClaimingThreefold
  } = useMutation({
    ...postRoomsByRoomIdGameDrawClaimThreefoldMutation(),
  })
  const {
    mutate: claimFifty,
    isPending: isClaimingFifty
  } = useMutation({
    ...postRoomsByRoomIdGameDrawClaimFiftyMutation(),
  })

  const handleRespondDrawOffer = (isAccepted: boolean) => {
    if (!myColor)
      return

    respondDraw({
      path: {roomId},
      body: {
        color: myColor,
        accepted: isAccepted
      }
    })
  }

  const pieces = new Map((board ?? []).map((item) => [`${item.column},${item.row}`, item]))
  const currentLegalMoves = legal && legal.moveCount === state?.moveCount ? legal.moves : []
  const legalMovesFromSelectedSquare = activeSquare
    ? currentLegalMoves.filter((m) => sameSquare(m.from, activeSquare))
    : []
  const legalTargetKeys = new Set(
    legalMovesFromSelectedSquare.map((m) => `${m.to.column},${m.to.row}`),
  )
  const loadingSquare = movingSquare ?? (legalPending && activeSquare && pieces.get(`${activeSquare.column},${activeSquare.row}`)?.type
    ? activeSquare
    : null)

  const capturedOf = (color: PieceColor) =>
    (color === 'White' ? state?.capturedByWhite : state?.capturedByBlack) ?? []

  const flip = myColor === 'Black'
  const moves = history ?? []

  const handleSquareClick = (position: Position) => {
    if (state?.state !== 'InProgress' || isMoving || !myTurn)
      return

    const key = `${position.column},${position.row}`
    if (activeSquare && legalTargetKeys.has(key)) {
      const promoteOptions = legalMovesFromSelectedSquare.flatMap((move) =>
        sameSquare(move.to, position) && move.promotionType ? [move.promotionType] : [],
      )
      if (promoteOptions.length > 0) {
        setPendingPromotion({
          from: activeSquare,
          to: position,
          options: promoteOptions
        })

        return
      }

      setMovingSquare(activeSquare)
      makeMove({
        path: {roomId},
        body: {
          from: activeSquare,
          to: position,
          promotionType: null
        },
        query: myColor ? {color: myColor} : undefined,
      })
      setSelectedSquare(null)

      return
    }

    const piece = pieces.get(key)
    if (piece?.color && piece.color === turn) {
      setSelectedSquare(sameSquare(selectedSquare, position) ? null : position)
    } else {
      setSelectedSquare(null)
    }
  }

  const handlePromote = (type: PieceType) => {
    if (!pendingPromotion)
      return

    setMovingSquare(pendingPromotion.from)
    makeMove({
      path: {roomId},
      body: {
        from: pendingPromotion.from,
        to: pendingPromotion.to,
        promotionType: type,
      },
      query: myColor ? {color: myColor} : undefined,
    })

    setPendingPromotion(null)
    setSelectedSquare(null)
  }


  if (roomError) {
    return <RoomNotFound onBack={() => void navigate({to: '/'})}/>
  }

  if (!room) {
    return <RoomLoading/>
  }

  if (waiting) {
    return (
      <RoomWaiting
        roomId={roomId}
        playerLabel={myColor ? playerLabel(myColor, room.whiteName, room.blackName) : undefined}
        onBack={() => void navigate({to: '/'})}
      />
    )
  }

  return (
    <Shell fill>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand/>
      </header>

      <main
        className="grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_18rem] md:items-start lg:grid-cols-[minmax(0,1fr)_20rem]">
        <ChessBoard
          tiles={board ?? []}
          selectedSquare={activeSquare}
          pendingSquare={loadingSquare}
          legalTargets={legalTargetKeys}
          flipped={flip}
          lastMove={moves.at(-1)?.move}
          disabled={state?.state !== 'InProgress' || isMoving || !myTurn}
          checkedColor={state?.checkedColor}
          onSquareSelect={handleSquareClick}
        />

        <aside className="flex flex-col gap-4">
          <PlayerTurnCard
            roomId={roomId}
            online={online}
            state={state}
            whiteName={room?.whiteName}
            blackName={room?.blackName}
            myColor={myColor}
            onPlayerClick={(color) => setCapturedDialog({
              open: true,
              color: color
            })}
          />

          <div className="flex flex-col gap-2">
            <div className="flex gap-2">
              {canAct && (
                <ConfirmDialog
                  className="flex-1"
                  label="Game baru"
                  disabled={isStarting}
                  title="Mulai game baru?"
                  description="Papan dan riwayat langkah akan direset dari awal."
                  confirmLabel="Ya, mulai baru"
                  onConfirm={() => start({path: {roomId}})}
                />
              )}
              <ConfirmDialog
                className="flex-1"
                label="Lobby"
                variant="outline"
                title="Keluar ke lobby?"
                description="Room ini tetap hidup — kamu bisa masuk lagi pakai kode room."
                confirmLabel="Keluar ke lobby"
                onConfirm={() => void navigate({to: '/'})}
              />
            </div>
            {!over && canAct && resignColor && (
              <ConfirmDialog
                className="w-full"
                label={`${colorName(resignColor)} menyerah`}
                variant="destructive"
                disabled={isResigning}
                title="Menyerah?"
                description={`${colorName(resignColor)} akan dinyatakan kalah dan game selesai.`}
                confirmLabel="Ya, menyerah"
                confirmVariant="destructive"
                onConfirm={() =>
                  resign({
                    path: {roomId},
                    body: {color: resignColor},
                  })
                }
              />
            )}
            {online && !over && myColor && !drawOffer && (
              <ConfirmDialog
                className="w-full"
                label="Tawarkan draw"
                variant="outline"
                disabled={isOfferingDraw}
                title="Tawarkan draw?"
                description="Lawan bisa menerima untuk mengakhiri game seri, atau menolak."
                confirmLabel="Ya, tawarkan"
                onConfirm={() => offerDraw({path: {roomId}, body: {color: myColor}})}
              />
            )}
            {!over && canAct && myTurn && resignColor && state?.canClaimThreefoldRepetition && (
              <ConfirmDialog
                className="w-full"
                label="Klaim repetisi 3×"
                variant="outline"
                disabled={isClaimingThreefold}
                title="Klaim seri (repetisi 3×)?"
                description="Posisi yang sama sudah muncul tiga kali. Game akan berakhir seri."
                confirmLabel="Ya, klaim seri"
                onConfirm={() =>
                  claimThreefold({path: {roomId}, body: {color: resignColor}})
                }
              />
            )}
            {!over && canAct && myTurn && resignColor && state?.canClaimFiftyMoveRule && (
              <ConfirmDialog
                className="w-full"
                label="Klaim 50 langkah"
                variant="outline"
                disabled={isClaimingFifty}
                title="Klaim seri (aturan 50 langkah)?"
                description="Lima puluh langkah tiap sisi telah berjalan tanpa langkah bidak atau tangkapan. Game akan berakhir seri."
                confirmLabel="Ya, klaim seri"
                onConfirm={() => claimFifty({path: {roomId}, body: {color: resignColor}})}
              />
            )}
          </div>

          <DrawOfferDialog
            open={!!drawOffer && !over}
            offerer={drawOffer ? playerLabel(drawOffer.color, room.whiteName, room.blackName) : 'Lawan'}
            pending={isRespondingDraw}
            onRespond={handleRespondDrawOffer}
            onDismiss={() => setDrawOffer(null)}
          />

          <PromotionDialog
            open={pendingPromotion !== null && !over}
            color={turn ?? 'White'}
            options={pendingPromotion?.options ?? []}
            pending={isMoving}
            onSelect={handlePromote}
            onDismiss={() => setPendingPromotion(null)}
          />

          <CapturedPiecesDialog
            open={capturedDialog.open}
            onOpenChange={(open) => setCapturedDialog({
              open: open,
              color: capturedDialog.color
            })}
            color={capturedDialog.color}
            playerName={playerName(capturedDialog.color, room.whiteName, room.blackName) ?? colorName(capturedDialog.color)}
            pieces={capturedDialog.color ? capturedOf(capturedDialog.color) : []}
          />

          <MoveHistoryCard history={moves}/>
        </aside>
      </main>
    </Shell>
  )
}
