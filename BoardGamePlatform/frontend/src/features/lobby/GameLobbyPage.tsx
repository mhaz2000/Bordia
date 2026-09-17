import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  PlayIcon,
  TrophyIcon,
  BoltIcon,
  ShieldCheckIcon,
  ArrowPathIcon,
  ClockIcon,
  FlagIcon,
  KeyIcon,
  UserGroupIcon,
  PlusIcon,
  LockClosedIcon,
  ArrowLeftIcon,
} from '@heroicons/react/24/outline'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { gameApi, lobbyApi } from '@/shared/api/client'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Select } from '@/shared/components/Select'
import { Modal } from '@/shared/components/Modal'
import { Card, CardContent } from '@/shared/components/Card'
import { BrandLogo } from '@/shared/components/BrandLogo'
import { UnoCardVisual, UnoCardBackVisual } from '@/shared/components/UnoCardVisual'
import { SilverCardVisual, SilverCardBack } from '@/shared/components/SilverCardVisual'
import { MoonIcon as MoonSolid } from '@heroicons/react/24/solid'
import { GemChip, SplendorCardVisual } from '@/shared/components/SplendorCardVisual'
import { AzulFactoryDisc, AzulTile } from '@/shared/components/AzulBoardVisuals'
import { SplendorNobleVisual } from '@/shared/components/SplendorNobleVisual'
import { cardById, nobleById } from '@/features/game/splendor'
import { useI18n } from '@/i18n/I18nProvider'
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher'
import { useGameInfo, type RuleIconName } from './gameMeta'
import { RoomCard } from './RoomCard'
import { ActiveGameCard } from './ActiveGameCard'
import type { LobbyRoom } from '@/shared/api/lobby'

const RULE_ICONS: Record<RuleIconName, typeof PlayIcon> = {
  cards: PlayIcon,
  trophy: TrophyIcon,
  bolt: BoltIcon,
  shield: ShieldCheckIcon,
  refresh: ArrowPathIcon,
  clock: ClockIcon,
  flag: FlagIcon,
}

const ACTION_CARD_IDS: { color: number; value: number }[] = [
  { color: 1, value: 10 },
  { color: 3, value: 11 },
  { color: 0, value: 12 },
  { color: 4, value: 13 },
  { color: 4, value: 14 },
]

export function GameLobbyPage() {
  const { gameType } = useParams<{ gameType: string }>()
  const gameTypeParam = gameType ?? ''
  const info = useGameInfo(gameTypeParam)
  const isUno = gameTypeParam === 'UNO'
  const isSilver = gameTypeParam === 'Silver'
  const isSplendor = gameTypeParam === 'Splendor'
  const isAzul = gameTypeParam === 'Azul'
  const { t } = useI18n()
  const navigate = useNavigate()
  const { createRoom, joinRoom, joinRoomByCode, joinRoomByCodePending, isCreating } = useLobby()
  const { user } = useAuth()

  const { data: games = [] } = useQuery({
    queryKey: ['games'],
    queryFn: gameApi.listGames,
    staleTime: 1000 * 60 * 5,
  })
  const catalogGame = games.find((g) => g.gameType === gameTypeParam)

  const { data: rooms = [], isLoading: roomsLoading } = useQuery({
    queryKey: ['lobby', 'rooms'],
    queryFn: lobbyApi.listRooms,
    refetchInterval: 5000,
    refetchIntervalInBackground: false,
  })
  const gameRooms = rooms.filter((r) => r.gameType === gameTypeParam)
  const seatedPlayers = gameRooms.reduce((sum, r) => sum + r.players.length, 0)

  const { data: mySessions = [] } = useQuery({
    queryKey: ['game', 'mySessions'],
    queryFn: gameApi.mySessions,
    refetchInterval: 15000,
    refetchIntervalInBackground: false,
    staleTime: 5000,
  })
  const activeGames = mySessions.filter((s) => s.status === 'Active' && s.gameType === gameTypeParam)

  const [showCreateModal, setShowCreateModal] = useState(false)
  const [name, setName] = useState('')
  const [maxPlayers, setMaxPlayers] = useState(2)
  const [isPrivate, setIsPrivate] = useState(false)
  const [createError, setCreateError] = useState('')
  const [joinError, setJoinError] = useState('')
  const [joinCode, setJoinCode] = useState('')

  const openCreateModal = () => {
    setName('')
    setMaxPlayers(catalogGame?.minPlayers ?? 2)
    setIsPrivate(false)
    setCreateError('')
    setShowCreateModal(true)
  }

  const handleCreateRoom = async (e: React.FormEvent) => {
    e.preventDefault()
    setCreateError('')
    if (!name.trim()) {
      setCreateError(t('gameLobby.enterName'))
      return
    }
    if (!catalogGame) {
      setCreateError(t('gameLobby.catalogLoading'))
      return
    }
    try {
      const room = await createRoom({ name, gameType: gameTypeParam, maxPlayers, isPrivate })
      setShowCreateModal(false)
      setName('')
      navigate(`/lobby/${room.id}`)
    } catch (err) {
      setCreateError(err instanceof Error ? err.message : t('gameLobby.createFailed'))
    }
  }

  const handleJoinRoom = async (room: LobbyRoom) => {
    setJoinError('')
    const isMember = room.players.some((p) => p.userId === user?.id)
    try {
      if (!isMember) {
        await joinRoom(room.id)
      }
      navigate(`/lobby/${room.id}`)
    } catch (err) {
      setJoinError(err instanceof Error ? err.message : t('gameLobby.joinFailed'))
    }
  }

  const handleJoinByCode = async () => {
    const code = joinCode.trim().toUpperCase()
    if (code.length !== 6) {
      setJoinError(t('gameLobby.joinCodeInvalid'))
      return
    }
    setJoinError('')
    try {
      const room = await joinRoomByCode(code)
      setJoinCode('')
      navigate(`/lobby/${room.id}`)
    } catch (err) {
      setJoinError(err instanceof Error ? err.message : t('gameLobby.joinFailed'))
    }
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-slate-950/85 backdrop-blur border-b border-white/10 sticky top-0 z-10 shadow-lg shadow-slate-950/20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3">
              <Link
                to="/lobby"
                className="flex h-9 w-9 items-center justify-center rounded-lg text-white/75 transition-colors hover:bg-white/10 hover:text-white"
                aria-label={t('common.backToLobby')}
              >
                <ArrowLeftIcon className="w-5 h-5 rtl:-scale-x-100" />
              </Link>
              <BrandLogo dark compact />
              <span className="rounded-full bg-white/10 px-3 py-1 text-sm font-semibold text-white ring-1 ring-white/15">
                {info.title}
              </span>
            </div>
            {!info.comingSoon && (
              <div className="flex items-center gap-3">
                <LanguageSwitcher dark />
                <Button
                  variant="primary"
                  onClick={openCreateModal}
                  isLoading={isCreating}
                  className="bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700"
                >
                  <PlusIcon className="w-5 h-5 me-2" />
                  {t('gameLobby.createRoomBtn', { title: info.title })}
                </Button>
              </div>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-10">
        {/* ============================ HERO ============================ */}
        <section className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-emerald-900 via-emerald-950 to-black shadow-2xl">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(16,185,129,0.35),transparent_55%)]" />
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(239,68,68,0.18),transparent_50%)]" />

          {/* Decorative cards (physical sides flip per direction so they always
              sit on the trailing side, opposite the text block) */}
          {isUno && (
            <>
              <div className="pointer-events-none absolute -left-6 rtl:-left-auto rtl:-right-6 top-8 hidden sm:block">
                <div className="relative rtl:-scale-x-100">
                  <UnoCardBackVisual className="absolute -left-10 top-2 rotate-[-24deg] opacity-70" />
                  <div className="animate-floaty" style={{ '--rot': '-14deg' } as React.CSSProperties}>
                    <UnoCardVisual card={{ Color: 0, Value: 7 }} size="lg" className="rotate-[-14deg]" />
                  </div>
                </div>
              </div>
              <div className="pointer-events-none absolute right-6 rtl:right-auto rtl:left-6 top-6 hidden md:flex gap-3">
                <div className="animate-floaty-delayed" style={{ '--rot': '10deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 4, Value: 13 }} size="lg" className="rotate-[10deg]" />
                </div>
                <div className="animate-floaty mt-8" style={{ '--rot': '18deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 3, Value: 12 }} size="lg" className="rotate-[18deg]" />
                </div>
                <div className="animate-floaty-delayed mt-16" style={{ '--rot': '26deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 1, Value: 5 }} size="lg" className="rotate-[26deg]" />
                </div>
              </div>
            </>
          )}
          {isSilver && (
            <>
              {/* Silver: the amulet moon over hidden village cards */}
              <div className="pointer-events-none absolute -start-3 top-9 hidden sm:block">
                <div className="relative">
                  <SilverCardBack size="xl" className="absolute start-0 top-3 -rotate-[22deg] opacity-50" />
                  <div className="animate-floaty">
                    <SilverCardVisual
                      card={{ Id: 'hero-seer', Value: 8, FaceUp: true, Protected: false, AmuletProtected: false, GuardedByCardId: null }}
                      size="xl"
                      className="relative -rotate-[12deg] shadow-[0_0_30px_rgba(148,163,184,0.35)]"
                    />
                  </div>
                </div>
              </div>
              <div className="pointer-events-none absolute end-8 top-8 hidden flex-col items-center md:flex">
                <MoonSolid className="a-glyph h-16 w-16 text-slate-100 drop-shadow-[0_0_22px_rgba(226,232,240,0.85)]" />
                <div className="mt-5 flex items-end gap-2">
                  <div className="animate-floaty-delayed">
                    <SilverCardVisual
                      card={{ Id: 'hero-witch', Value: 11, FaceUp: true, Protected: false, AmuletProtected: false, GuardedByCardId: null }}
                      size="lg"
                      className="rotate-[8deg]"
                    />
                  </div>
                  <div className="animate-floaty mt-6" style={{ animationDelay: '1.4s' }}>
                    <SilverCardVisual
                      card={{ Id: 'hero-guard', Value: 3, FaceUp: true, Protected: false, AmuletProtected: false, GuardedByCardId: null }}
                      size="lg"
                      className="rotate-[16deg]"
                    />
                  </div>
                </div>
              </div>
            </>
          )}
          {isSplendor && (
            <>
              {/* Splendor: gem token cluster beside a jeweler card and a noble tile */}
              <div className="pointer-events-none absolute -start-1 top-9 hidden flex-col gap-3 sm:flex">
                <div className="animate-floaty self-start">
                  <GemChip color="ruby" size="lg" className="rotate-[-10deg] shadow-[0_0_18px_rgba(244,63,94,0.45)]" />
                </div>
                <div className="animate-floaty-delayed self-center">
                  <GemChip color="emerald" size="lg" />
                </div>
                <div className="animate-floaty self-start" style={{ animationDelay: '1.8s' }}>
                  <GemChip color="sapphire" size="lg" className="rotate-[12deg]" />
                </div>
              </div>
              <div className="pointer-events-none absolute end-6 top-8 hidden items-start gap-3 md:flex">
                <div className="animate-floaty-delayed">
                  {cardById('L3R04') && <SplendorCardVisual card={cardById('L3R04')!} size="lg" className="rotate-[-8deg]" />}
                </div>
                <div className="mt-10 animate-floaty" style={{ animationDelay: '0.9s' }}>
                  {nobleById('N-DSE') && <SplendorNobleVisual noble={nobleById('N-DSE')!} size="sm" />}
                </div>
              </div>
            </>
          )}
          {isAzul && (
            <>
              {/* Azul: factory discs and floating azulejo tiles */}
              <div className="pointer-events-none absolute -start-3 top-9 hidden sm:block">
                <div className="animate-floaty">
                  <AzulFactoryDisc tiles={[0, 1, 2, 3]} index={0} />
                </div>
              </div>
              <div className="pointer-events-none absolute end-6 top-8 hidden items-start gap-2 md:flex">
                <div className="animate-floaty-delayed">
                  <AzulTile color={0} size="lg" className="rotate-[-8deg] shadow-2xl" />
                </div>
                <div className="mt-6 animate-floaty" style={{ animationDelay: '0.6s' }}>
                  <AzulTile color={4} size="lg" className="rotate-[6deg] shadow-2xl" />
                </div>
                <div className="mt-12 animate-floaty-delayed" style={{ animationDelay: '1.3s' }}>
                  <AzulTile color={1} size="lg" className="rotate-[14deg] shadow-2xl" />
                </div>
                <div className="mt-2 animate-floaty" style={{ animationDelay: '1.8s' }}>
                  <AzulFactoryDisc tiles={[2, 4, 3]} index={1} />
                </div>
              </div>
            </>
          )}
          {!isUno && !isSilver && !isSplendor && !isAzul && (
            <div className="pointer-events-none absolute end-8 top-8 hidden md:block opacity-20">
              <span className="text-[10rem] font-black text-white leading-none">{info.title.charAt(0)}</span>
            </div>
          )}

          <div className="relative px-6 py-12 sm:px-12 sm:py-16 max-w-2xl">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full bg-white/10 px-3 py-1 text-[11px] font-bold uppercase tracking-widest text-emerald-200 backdrop-blur-sm">
                {t('gameLobby.cardGame')}
              </span>
              {catalogGame && (
                <span className="inline-flex items-center gap-1 rounded-full bg-white/10 px-3 py-1 text-[11px] font-bold uppercase tracking-widest text-white/80 backdrop-blur-sm">
                  <UserGroupIcon className="w-3.5 h-3.5" />
                  {t('lobby.playersRange', { min: catalogGame.minPlayers, max: catalogGame.maxPlayers })}
                </span>
              )}
            </div>
            <h2 className="mt-4 text-5xl sm:text-6xl font-black italic tracking-tighter text-white drop-shadow-[0_4px_12px_rgba(0,0,0,0.5)]">
              {info.title.toUpperCase()}
            </h2>
            <p className="mt-2 text-lg font-semibold text-emerald-200">{info.tagline}</p>
            <p className="mt-3 text-sm sm:text-base leading-relaxed text-white/75 max-w-xl">{info.description}</p>
            {!info.comingSoon && (
              <div className="mt-6 flex flex-wrap gap-3">
                <Button onClick={openCreateModal} isLoading={isCreating} className="!bg-white !text-emerald-900 hover:!bg-emerald-50 border-none shadow-lg">
                  <PlusIcon className="w-5 h-5 mr-1.5" />
                  {t('gameLobby.createTable')}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => document.getElementById('open-rooms')?.scrollIntoView({ behavior: 'smooth' })}
                  className="!text-white border !border-white/30 hover:!bg-white/10"
                >
                  {t('gameLobby.browseRooms')}
                </Button>
              </div>
            )}
          </div>
        </section>

        {/* ============================ STATS ============================ */}
        {!info.comingSoon && (
          <section className="grid grid-cols-3 gap-4">
            <StatTile label={t('gameLobby.statOpenRooms')} value={gameRooms.length} />
            <StatTile label={t('gameLobby.statSeated')} value={seatedPlayers} />
            <StatTile label={t('gameLobby.statLive')} value={activeGames.length} />
          </section>
        )}

        {joinError && (
          <div className="p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg" role="alert">
            {joinError}
          </div>
        )}

        {/* ============================ RULES ============================ */}
        <section className="space-y-4">
          <SectionTitle title={t('gameLobby.howToPlay')} />
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {info.rules.map((rule, i) => {
              const Icon = RULE_ICONS[rule.icon as RuleIconName] ?? PlayIcon
              return (
                <Card key={i} className="hover:shadow-md transition-shadow">
                  <CardContent className="flex gap-4">
                    <div className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white shadow">
                      <Icon className="h-5 w-5" />
                    </div>
                    <div>
                      <h3 className="font-semibold text-gray-900 text-sm">{rule.title}</h3>
                      <p className="mt-1 text-sm text-gray-500 leading-relaxed">{rule.detail}</p>
                    </div>
                  </CardContent>
                </Card>
              )
            })}
          </div>
        </section>

        {/* ============================ ACTION CARDS (UNO) ============================ */}
        {isUno && info.actionCards.length > 0 && (
          <section className="space-y-4">
            <SectionTitle title={t('gameLobby.meetActionCards')} />
            <div className="rounded-3xl bg-gradient-to-br from-slate-800 via-slate-900 to-black p-6 sm:p-8 shadow-inner">
              <div className="grid grid-cols-2 gap-6 sm:grid-cols-3 lg:grid-cols-5">
                {info.actionCards.map((c, i) => {
                  const ids = ACTION_CARD_IDS[i]
                  return (
                    <div key={i} className="flex flex-col items-center gap-3 text-center">
                      <div className="transition-transform duration-200 hover:-translate-y-2 hover:scale-105">
                        <UnoCardVisual card={{ Color: ids.color, Value: ids.value }} size="lg" />
                      </div>
                      <div>
                        <p className="text-sm font-bold text-white">{c.name}</p>
                        <p className="mt-1 text-xs text-white/50 leading-snug">{c.blurb}</p>
                      </div>
                    </div>
                  )
                })}
              </div>
            </div>
          </section>
        )}

        {info.comingSoon ? (
          <Card>
            <CardContent className="py-12 text-center">
              <LockClosedIcon className="mx-auto h-10 w-10 text-gray-400" />
              <h3 className="mt-2 text-lg font-medium text-gray-900">{t('gameLobby.comingSoonTitle', { title: info.title })}</h3>
              <p className="mt-1 text-gray-500">{t('gameLobby.comingSoonDetail')}</p>
              <Button variant="primary" className="mt-4" asChild>
                <Link to="/lobby">{t(`common.backToLobby`)}</Link>
              </Button>
            </CardContent>
          </Card>
        ) : (
          <>
            {/* Active games of this type */}
            {activeGames.length > 0 && (
              <section className="space-y-3">
                <SectionTitle title={t('gameLobby.yourLiveGames', { title: info.title })} pulse />
                {activeGames.map((session) => (
                  <ActiveGameCard
                    key={session.id}
                    session={session}
                    currentUserId={user?.id}
                    onRejoin={() => navigate(`/game/${session.id}`)}
                  />
                ))}
              </section>
            )}

            {/* Open rooms */}
            <section id="open-rooms" className="space-y-4 scroll-mt-24">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <SectionTitle title={t('gameLobby.openTables')} pulse />
                <div className="flex flex-wrap items-center gap-2">
                  <div className="flex items-center gap-2 rounded-xl border border-gray-200 bg-white p-1 ps-3 shadow-sm">
                    <KeyIcon className="h-4 w-4 text-gray-400" />
                    <input
                      value={joinCode}
                      onChange={(e) => setJoinCode(e.target.value.toUpperCase())}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault()
                          handleJoinByCode()
                        }
                      }}
                      maxLength={6}
                      placeholder={t('gameLobby.joinCodePlaceholder')}
                      aria-label={t('gameLobby.joinCodeBtn')}
                      className="w-24 bg-transparent py-1 font-mono text-sm font-bold tracking-widest text-gray-900 uppercase placeholder:font-normal placeholder:tracking-normal placeholder:text-gray-400 placeholder:uppercase focus:outline-none"
                    />
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={handleJoinByCode}
                      isLoading={joinRoomByCodePending}
                    >
                      {t('gameLobby.joinCodeBtn')}
                    </Button>
                  </div>
                  <Button variant="secondary" size="sm" onClick={openCreateModal} isLoading={isCreating}>
                    <PlusIcon className="w-4 h-4 me-1" />
                    {t('gameLobby.newRoom')}
                  </Button>
                </div>
              </div>
              <p className="text-xs text-gray-400">{t('gameLobby.joinCodeHint')}</p>

              {roomsLoading && gameRooms.length === 0 ? (
                <div className="flex justify-center py-12">
                  <div className="animate-spin rounded-full h-10 w-10 border-4 border-blue-600 border-t-transparent" />
                </div>
              ) : gameRooms.length === 0 ? (
                <Card>
                  <CardContent className="py-14 text-center">
                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-50">
                      <UserGroupIcon className="h-7 w-7 text-blue-500" />
                    </div>
                    <h3 className="mt-4 text-lg font-semibold text-gray-900">{t('gameLobby.noTablesYet', { title: info.title })}</h3>
                    <p className="mt-1 text-gray-500">{t('gameLobby.beHost')}</p>
                    <Button variant="primary" className="mt-5" onClick={openCreateModal} isLoading={isCreating}>
                      <PlusIcon className="w-5 h-5 me-2" />
                      {t('gameLobby.createRoomBtn', { title: info.title })}
                    </Button>
                  </CardContent>
                </Card>
              ) : (
                <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                  {gameRooms.map((room) => (
                    <RoomCard key={room.id} room={room} currentUserId={user?.id} onSelect={handleJoinRoom} />
                  ))}
                </div>
              )}
            </section>
          </>
        )}
      </main>

      {/* Create room modal (game type locked to this page) */}
      <Modal isOpen={showCreateModal} onClose={() => setShowCreateModal(false)} title={t('gameLobby.createRoomTitle', { title: info.title })}>
        <form onSubmit={handleCreateRoom} noValidate className="space-y-4">
          {createError && (
            <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm" role="alert">
              {createError}
            </div>
          )}
          <Input
            label={t('gameLobby.roomName')}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder={t('gameLobby.roomNamePlaceholder', { title: info.title })}
            maxLength={50}
          />
          <Select
            label={t('gameLobby.maxPlayers')}
            value={maxPlayers}
            onChange={(e) => setMaxPlayers(Number(e.target.value))}
            disabled={!catalogGame}
          >
            {catalogGame &&
              Array.from(
                { length: catalogGame.maxPlayers - catalogGame.minPlayers + 1 },
                (_, index) => catalogGame.minPlayers + index
              ).map((n) => (
                <option key={n} value={n}>
                  {t('gameLobby.playersOption', { n })}
                </option>
              ))}
          </Select>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isPrivateGame"
              checked={isPrivate}
              onChange={(e) => setIsPrivate(e.target.checked)}
              className="h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
            />
            <label htmlFor="isPrivateGame" className="text-sm text-gray-700">
              {t('gameLobby.privateRoom')}
            </label>
          </div>
          <div className="flex gap-3 pt-2">
            <Button variant="secondary" type="button" onClick={() => setShowCreateModal(false)} className="flex-1">
              {t('common.cancel')}
            </Button>
            <Button type="submit" isLoading={isCreating} disabled={!catalogGame} className="flex-1">
              {t('common.create')}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}

function StatTile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl bg-white border border-gray-200 px-5 py-4 shadow-sm">
      <p className="text-3xl font-black text-gray-900 tabular-nums">{value}</p>
      <p className="mt-0.5 text-xs font-semibold uppercase tracking-wide text-gray-400">{label}</p>
    </div>
  )
}

function SectionTitle({ title, pulse }: { title: string; pulse?: boolean }) {
  return (
    <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
      {pulse && <span className="relative flex h-2 w-2">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
        <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
      </span>}
      {title}
    </h2>
  )
}

