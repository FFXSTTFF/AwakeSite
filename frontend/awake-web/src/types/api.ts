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

export interface RegisterResponse {
  userId: string
  username: string
}

export interface SquadMemberDto {
  userId: string
  username: string
  gameNickname: string | null
  isLeader: boolean
  joinedAt: string
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

export interface TicketDetailDto extends TicketListItemDto {
  description: string
  reviewedAt: string | null
  reviewedByUsername: string | null
  comments: TicketCommentDto[]
  playerData: unknown | null
}
