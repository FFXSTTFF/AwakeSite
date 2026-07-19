export const UserRank = {
  Guest: 0,
  Member: 1,
  Officer: 2,
  Colonel: 3,
  Leader: 4,
} as const

export type UserRank = (typeof UserRank)[keyof typeof UserRank]

export interface CurrentUser {
  userId: string
  username: string
  rank: UserRank
}

export interface LoginResponse {
  accessToken: string
  username: string
  rank: UserRank
  userId: string
}

export interface SquadMemberDto {
  userId: string
  username: string
  gameNickname: string | null
  isLeader: boolean
  joinedAt: string
  flags: PlayerFlags
  kd: number | null
  boosts: BoostItem[]
}

export interface SquadDto {
  id: string
  name: string
  number: number
  members: SquadMemberDto[]
  memberCount: number
}

export interface UserDto {
  id: string
  username: string
  email: string | null
  gameNickname: string | null
  rank: UserRank
  createdAt: string
}

export const TicketStatus = {
  Pending: 0,
  InReview: 1,
  Approved: 2,
  Rejected: 3,
  Closed: 4,
} as const
export type TicketStatus = (typeof TicketStatus)[keyof typeof TicketStatus]

export const TicketType = {
  Recruitment: 0,
  Appeal: 1,
} as const
export type TicketType = (typeof TicketType)[keyof typeof TicketType]

export interface TicketCommentDto {
  id: string
  authorUsername: string
  content: string
  createdAt: string
}

export interface TicketListItemDto {
  id: string
  type: TicketType
  status: TicketStatus
  gameNickname: string
  authorUsername: string
  createdAt: string
}

export interface LoadoutSlot {
  itemId: string
  itemName: string
  itemIcon: string
  upgrade: number
}

export interface Loadout {
  sniper: LoadoutSlot | null
  weapon: LoadoutSlot
  armor: LoadoutSlot
}

export interface LoadoutSlotRequest {
  itemId: string
  upgrade: number
}

export interface UpdateLoadoutRequest {
  sniper: LoadoutSlotRequest | null
  weapon: LoadoutSlotRequest
  armor: LoadoutSlotRequest
}

export interface ClanEntry {
  clanName: string
  clanTag: string
  since: string
}

export interface PlayerProfile {
  kills: number
  deaths: number
  kdRatio: number
  accuracy: string
  playtime: string
  clanHistory: ClanEntry[]
}

export interface ItemSearchResult {
  id: string
  category: string
  nameRu: string
  icon: string
  color: string
}

export interface PlayerSquadDto {
  id: string
  name: string
  number: number
  isLeader: boolean
}

export interface PlayerStatsDto {
  kills: number
  deaths: number
  kdRatio: number
  accuracy: string
  playtime: string
  clanHistory: ClanEntry[]
  fetchedAt: string
}

export interface PlayerProfileDto {
  userId: string
  username: string
  discordUsername: string | null
  discordAvatarUrl: string | null
  rank: UserRank
  gameNickname: string | null
  squad: PlayerSquadDto | null
  stats: PlayerStatsDto | null
  loadout: Loadout | null
  boosts: BoostItem[]
}

export interface TicketDetailDto extends TicketListItemDto {
  description: string
  reviewedAt: string | null
  reviewedByUsername: string | null
  comments: TicketCommentDto[]
  playerData: PlayerProfile | null
  loadout: Loadout | null
}

export interface LeaderboardEntryDto {
  gameNickname: string
  kills: number
  accuracy: string
  playtime: string
}

export const BuildType = {
  Speed: 0,
  Vitality: 1,
} as const
export type BuildType = (typeof BuildType)[keyof typeof BuildType]

export const BoostType = {
  Damage: 0,
  ShortDamage: 1,
  Speed: 2,
  Defense: 3,
} as const
export type BoostType = (typeof BoostType)[keyof typeof BoostType]

export const ALL_BOOST_TYPES: readonly BoostType[] = [
  BoostType.Damage,
  BoostType.ShortDamage,
  BoostType.Speed,
  BoostType.Defense,
]

export interface BoostItem {
  boostType: BoostType
  itemId: string
  name: string
  icon: string | null
}

export interface BoostSelection {
  boostType: BoostType
  itemId: string
}

export interface BoostSummaryEntry {
  userId: string
  username: string
  gameNickname: string | null
  boosts: BoostItem[]
}

export interface PlayerFlags {
  bio: boolean
  combat: boolean
  sniper: boolean
  speed: boolean
  vitality: boolean
}

export interface InventoryItem {
  itemId: string
  name: string
  icon: string | null
  color: string | null
  category: string | null
  unknown: boolean
}

export interface PlayerInventory {
  items: InventoryItem[]
  flags: PlayerFlags
}

export interface BuilderFighter {
  userId: string
  username: string
  gameNickname: string | null
  avatarUrl: string | null
  flags: PlayerFlags
  kd: number | null
}

export interface BuilderSquad {
  id: string
  name: string
  number: number
  members: BuilderFighter[]
}

export interface SquadBuilderData {
  squads: BuilderSquad[]
  pool: BuilderFighter[]
}
