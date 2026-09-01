-- Agent studio (v2): the visual flow behind an agent.
--
-- One JSONB column, not a nodes table plus an edges table: the graph is only ever
-- read and written whole, by one screen, and Postgres can index into it if that
-- ever changes. {"nodes":[{id,kind,label,x,y}],"edges":[[fromId,toId]]}
ALTER TABLE agent ADD COLUMN flow JSONB NOT NULL DEFAULT '{"nodes":[],"edges":[]}'::jsonb;
