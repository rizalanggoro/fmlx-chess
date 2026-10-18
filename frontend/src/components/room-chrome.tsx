import { BoardBand } from '@/components/board-band'
import type { ReactNode } from 'react'

export function Shell({ children, fill = false }: { children: ReactNode; fill?: boolean }) {
  return (
    <div
      className={`relative flex min-h-svh flex-col items-center overflow-clip bg-amber-50 px-4 sm:px-6 ${
        fill ? 'py-6 md:py-8' : 'justify-center py-10 md:py-14'
      }`}
    >
      <span
        aria-hidden
        className="pointer-events-none absolute -right-12 -bottom-16 hidden font-heading text-[22rem] leading-none text-amber-900/5 select-none lg:block"
      >
        ♞
      </span>
      <div
        className={`relative flex w-full flex-col gap-6 ${fill ? 'max-w-6xl' : 'max-w-5xl'}`}
      >
        {children}
      </div>
    </div>
  )
}

export function Brand() {
  return (
    <div className="flex flex-col items-start gap-3">
      <p className="font-heading text-3xl leading-none tracking-tight text-amber-950 sm:text-4xl">
        fmlx<span className="text-amber-700">-</span>chess
      </p>
      <BoardBand />
    </div>
  )
}

export function Eyebrow({ children }: { children: ReactNode }) {
  return (
    <p className="text-xs font-semibold tracking-[0.25em] text-amber-800/80 uppercase">{children}</p>
  )
}
