create extension if not exists pgcrypto;

create table if not exists public.households (
    id uuid primary key,
    name text not null,
    invite_code text not null unique,
    created_by_user_id uuid not null,
    updated_at_utc timestamptz not null default timezone('utc', now())
);

create table if not exists public.profiles (
    id uuid primary key,
    household_id uuid null references public.households(id) on delete set null,
    display_name text not null,
    email text not null unique,
    accent_hex text null,
    updated_at_utc timestamptz not null default timezone('utc', now())
);

create table if not exists public.sync_documents (
    household_id uuid not null references public.households(id) on delete cascade,
    entity_name text not null,
    entity_id uuid not null,
    payload jsonb not null,
    updated_at_utc timestamptz not null default timezone('utc', now()),
    deleted_at_utc timestamptz null,
    primary key (household_id, entity_name, entity_id)
);

alter table public.households enable row level security;
alter table public.profiles enable row level security;
alter table public.sync_documents enable row level security;

create policy if not exists "households_select_authenticated"
on public.households
for select
to authenticated
using (true);

create policy if not exists "households_upsert_authenticated"
on public.households
for all
to authenticated
using (true)
with check (true);

create policy if not exists "profiles_select_authenticated"
on public.profiles
for select
to authenticated
using (true);

create policy if not exists "profiles_upsert_authenticated"
on public.profiles
for all
to authenticated
using (true)
with check (true);

create policy if not exists "sync_documents_select_authenticated"
on public.sync_documents
for select
to authenticated
using (true);

create policy if not exists "sync_documents_upsert_authenticated"
on public.sync_documents
for all
to authenticated
using (true)
with check (true);
