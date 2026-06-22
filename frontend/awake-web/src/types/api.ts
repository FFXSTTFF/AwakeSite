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
