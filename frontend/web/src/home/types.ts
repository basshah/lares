export interface Member {
  userId: string
  fullName: string
  email: string
  role: 'Owner' | 'Member'
  joinedAtUtc: string
}

export interface HomeSummary {
  id: string
  name: string
  role: 'Owner' | 'Member'
  inviteCode: string | null
  members: Member[]
}
