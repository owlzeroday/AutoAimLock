// get-license/index.ts
// Server-side license evaluation. The client calls this with the user's JWT.
// Returns the effective plan + expiry, or a 1-hour trial computed from account
// creation if no paid license exists yet.
import { createClient } from "jsr:@supabase/supabase-js@2";

const TRIAL_HOURS = 1;

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
);

type Plan = "trial" | "3days" | "7days" | "1month";
const PLAN_DURATION_DAYS: Record<Exclude<Plan, "trial">, number> = {
  "3days": 3,
  "7days": 7,
  "1month": 30,
};

Deno.serve(async (req) => {
  try {
    // Resolve the caller from the Authorization bearer JWT.
    const auth = req.headers.get("Authorization") ?? "";
    const token = auth.startsWith("Bearer ") ? auth.slice(7) : "";
    const { data: { user }, error: uerr } = await supabase.auth.getUser(token);
    if (uerr || !user) {
      return Response.json({ ok: false, error: "unauthorized" }, { status: 401 });
    }

    // Device fingerprint binds this license run to a machine.
    const body = await req.json().catch(() => ({}));
    const device = typeof body.device === "string" ? body.device : "";

    // 1) Active paid license?
    const { data: lic } = await supabase
      .from("licenses")
      .select("*")
      .eq("user_id", user.id)
      .eq("status", "active")
      .order("created_at", { ascending: false })
      .limit(1);

    const active = lic?.[0];
    const now = Date.now();
    if (active?.active_until) {
      const until = new Date(active.active_until).getTime();
      if (until > now) {
        return Response.json({
          ok: true,
          plan: active.plan,
          tier: "paid",
          expires_at: active.active_until,
          device,
        });
      }
    }

    // 2) No active paid license → trial (1 hour from account creation).
    const created = new Date(user.created_at).getTime();
    const trialEnd = created + TRIAL_HOURS * 3600_000;
    const trialActive = now < trialEnd;

    return Response.json({
      ok: true,
      plan: "trial",
      tier: trialActive ? "trial" : "expired",
      trial_started_at: user.created_at,
      expires_at: trialActive ? new Date(trialEnd).toISOString() : user.created_at,
      device,
    });
  } catch (err) {
    return Response.json({ ok: false, error: String(err) }, { status: 500 });
  }
});
