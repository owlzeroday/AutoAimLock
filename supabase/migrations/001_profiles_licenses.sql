-- 001_profiles_licenses.sql
-- AutoAimLock: profiles + licenses tables with RLS.

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  email text not null,
  trial_started_at timestamptz,
  created_at timestamptz not null default now()
);

create table if not exists public.licenses (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  plan text not null,                -- 'trial' | '3days' | '7days' | '1month'
  status text not null default 'active',  -- 'active' | 'disabled'
  active_from timestamptz not null default now(),
  active_until timestamptz,
  device_fingerprint text,           -- optional device binding
  created_at timestamptz not null default now()
);

-- A user can only see their own rows.
alter table public.profiles enable row level security;
alter table public.licenses enable row level security;

drop policy if exists "profiles_select_own" on public.profiles;
create policy "profiles_select_own" on public.profiles
  for select using (auth.uid() = id);

drop policy if exists "licenses_select_own" on public.licenses;
create policy "licenses_select_own" on public.licenses
  for select using (auth.uid() = user_id);

-- Grant API access.
grant select on public.profiles to authenticated;
grant select on public.licenses to authenticated;
