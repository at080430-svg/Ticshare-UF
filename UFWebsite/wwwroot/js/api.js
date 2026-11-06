// Small helper used by every page: calls our API and turns errors into readable text.
async function api(url, method = "GET", body = null) {
    const options = { method, headers: {} };
    if (body) {
        options.headers["Content-Type"] = "application/json";
        options.body = JSON.stringify(body);
    }
    const svar = await fetch(url, options);
    if (svar.status === 401) throw new Error("Du måste logga in först.");
    const text = await svar.text();
    const data = text ? JSON.parse(text) : null;
    if (!svar.ok) throw new Error(data?.error ?? data?.errors?.join(" ") ?? data?.detail ?? "Något gick fel.");
    return data;
}

// Text that users typed (names, seats, reasons) must never be put into innerHTML as-is,
// or someone could register the name "<script>...". esc() makes it harmless text.
function esc(text) {
    const div = document.createElement("div");
    div.textContent = text ?? "";
    return div.innerHTML;
}

function kronor(belopp) {
    return Number(belopp).toLocaleString("sv-SE") + " kr";
}

function datum(iso) {
    return new Date(iso).toLocaleString("sv-SE", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

// CSS class for a sport's colours, e.g. "Ishockey" -> "sport-ishockey" (see css/sporter.css).
function sportKlass(sport) {
    const kanda = ["fotboll", "ishockey", "basket", "innebandy"];
    const namn = (sport ?? "").toLowerCase();
    return kanda.includes(namn) ? `sport-${namn}` : "sport-standard";
}
