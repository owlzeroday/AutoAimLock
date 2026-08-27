// activate-license/index.ts
// Called by a payment webhook (Mangopay/QRPh) after confirmation, OR by an admin
// with a service-role key to provision a paid plan. Creates/extends a paid license.
import { createClient } from "jsr:@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
);

const PLAN_DURATION_DAYS: Record<string, number> = {
  "3days": 3,
  "7days": 7,
  "1month": 30,
};

Deno.serve(async (req) => {
  try {
    const body = await req.json();
    const { user_id, plan, device } = body;
    if (!user_id || !PLAN_DURATION_DAYS[plan]) {
      return Response.json({ ok: false, error: "bad_request" }, { status: 400 });
    }

    const days = PLAN_DURATION_DAYS[plan];
    const now = new Date();

    // Extend from current active_until if still valid, else from now.
    const { data: cur } = await supabase
      .from("licenses")
      .select("*")
      .eq("user_id", user_id)
      .eq("status", "active")
      .order("active_until", { ascending: false })
      .limit(1);

    let base = now;
    if (cur?.[0]?.active_until && new Date(cur[0].active_until).getTime() > now.getTime()) {
      base = new Date(cur[0].active_until);
    }
    const until = new Date(base.getTime() + days * 86400_000);

    const { data, error } = await supabase
      .from("licenses")
      .insert({
        user_id,
        plan,
        status: "active",
        active_from: now.toISOString(),
        active_until: until.toISOString(),
        device_fingerprint: device ?? null,
      })
      .select()
      .single();

    if (error) return Response.json({ ok: false, error: error.message }, { status: 500 });
    return Response.json({ ok: true, license: data });
  } catch (err) {
    return Response.json({ ok: false, error: String(err) }, { status: 500 });
  }
});
