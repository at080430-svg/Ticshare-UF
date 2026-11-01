const statusText = {
    // Orders
    AwaitingPayment: "Väntar på din Swish-betalning", Paid: "Betald, säljaren skickar biljetten",
    TicketSent: "Biljetten är skickad", Completed: "Klar", PaidOut: "Klar", Cancelled: "Avbruten", Refunded: "Återbetald",
    // Listings and season cards
    PendingReview: "Väntar på granskning", Approved: "Godkänt", Available: "Till salu", Reserved: "Reserverad",
    Sold: "Såld", Rejected: "Nekad", Blocked: "Spärrad",
};

async function start() {
    let jag;
    try { jag = await api("/api/auth/me"); }
    catch { location.href = "login.html"; return; }

    document.getElementById("namn").textContent = jag.fullName;
    document.getElementById("swish").value = jag.swishNumber ?? "";
    await Promise.all([visaKop(), visaForsaljningar(), visaKort(), fyllKlubbar()]);
}

async function visaKop() {
    const kop = await api("/api/orders/mine");
    document.getElementById("kop").innerHTML = kop.length === 0 ? "<p>Inga köp än.</p>" : kop.map(o => `
        <div class="rad">
            <p><strong>${esc(o.match)}</strong>, ${datum(o.startTime)}. Sektion ${esc(o.section)}, rad ${esc(o.row)}, plats ${esc(o.seat)}</p>
            <p>${kronor(o.totalAmount)} · Referens ${o.reference} · ${statusText[o.status]}</p>
            ${o.status === "TicketSent" ? `<button onclick="fattBiljett(${o.id})">Jag har fått biljetten</button>` : ""}
        </div>`).join("");
}

async function visaForsaljningar() {
    const lista = await api("/api/me/listings");
    document.getElementById("forsaljningar").innerHTML = lista.length === 0 ? "<p>Inga försäljningar än.</p>" : lista.map(l => `
        <div class="rad">
            <p><strong>${esc(l.match)}</strong>, ${datum(l.startTime)} · ${kronor(l.price)} · ${statusText[l.status]}
               ${l.rejectionReason ? `(${esc(l.rejectionReason)})` : ""}</p>
            ${l.order?.status === "Paid" ? `
                <p>Sålt till <strong>${esc(l.order.buyerName)}</strong> (${esc(l.order.buyerEmail)}).
                   För över biljetten till köparen i klubbens app och klicka sedan här:</p>
                <button onclick="biljettSkickad(${l.order.id})">Biljetten är skickad</button>` : ""}
            ${["PendingReview", "Available"].includes(l.status) ? `<button onclick="taBort(${l.id})">Ta bort</button>` : ""}
        </div>`).join("");
}

async function visaKort() {
    const kort = await api("/api/me/seasoncards");
    document.getElementById("kort").innerHTML = kort.length === 0 ? "<p>Inga säsongskort än.</p>" : kort.map(k => `
        <p>${esc(k.club)} ${esc(k.season)}, kort ${esc(k.cardNumber)}: ${statusText[k.status]} ${k.rejectionReason ? `(${esc(k.rejectionReason)})` : ""}</p>`).join("");

    // Only approved cards can be used to sell.
    const godkanda = kort.filter(k => k.status === "Approved");
    document.getElementById("salj-kort").innerHTML = godkanda.map(k =>
        `<option value="${k.id}" data-club="${k.clubId}">${esc(k.club)}, sektion ${esc(k.section)} rad ${esc(k.row)} plats ${esc(k.seat)}</option>`).join("");
    await fyllMatcher();
}

async function fyllKlubbar() {
    const klubbar = await api("/api/clubs");
    document.getElementById("kort-klubb").innerHTML = klubbar.filter(k => k.allowsResale)
        .map(k => `<option value="${k.id}">${esc(k.name)}</option>`).join("");
}

// The match list only shows home matches for the club of the chosen season card.
async function fyllMatcher() {
    const valt = document.getElementById("salj-kort").selectedOptions[0];
    if (!valt) { document.getElementById("salj-match").innerHTML = ""; return; }
    const matcher = await api("/api/matches");
    document.getElementById("salj-match").innerHTML = matcher
        .filter(m => m.homeClubId == valt.dataset.club)
        .map(m => `<option value="${m.id}">${esc(m.home)} – ${esc(m.away)}, ${datum(m.startTime)}</option>`).join("");
}

document.getElementById("salj-kort").addEventListener("change", fyllMatcher);

document.getElementById("salj-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const meddelande = document.getElementById("salj-meddelande");
    try {
        await api("/api/me/listings", "POST", {
            seasonCardId: Number(document.getElementById("salj-kort").value),
            matchId: Number(document.getElementById("salj-match").value),
            faceValue: Number(document.getElementById("salj-ordinarie").value),
            price: Number(document.getElementById("salj-pris").value),
        });
        meddelande.textContent = "Tack! Biljetten syns när en admin har godkänt den.";
        visaForsaljningar();
    } catch (fel) { meddelande.textContent = fel.message; }
});

document.getElementById("kort-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const meddelande = document.getElementById("kort-meddelande");
    try {
        await api("/api/me/seasoncards", "POST", {
            clubId: Number(document.getElementById("kort-klubb").value),
            season: document.getElementById("kort-sasong").value,
            cardNumber: document.getElementById("kort-nummer").value,
            section: document.getElementById("kort-sektion").value,
            row: document.getElementById("kort-rad").value,
            seat: document.getElementById("kort-plats").value,
            acceptedTerms: document.getElementById("kort-villkor").checked,
        });
        meddelande.textContent = "Tack! Vi kontrollerar kortet med klubben och hör av oss.";
        e.target.reset();
        visaKort();
    } catch (fel) { meddelande.textContent = fel.message; }
});

document.getElementById("swish-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    try {
        await api("/api/me/swish", "PUT", { swishNumber: document.getElementById("swish").value });
        alert("Sparat!");
    } catch (fel) { alert(fel.message); }
});

async function fattBiljett(orderId) {
    await api(`/api/orders/${orderId}/received`, "POST");
    visaKop();
}

async function biljettSkickad(orderId) {
    await api(`/api/me/sales/${orderId}/ticket-sent`, "POST");
    visaForsaljningar();
}

async function taBort(listingId) {
    if (!confirm("Ta bort biljetten från försäljning?")) return;
    try { await api(`/api/me/listings/${listingId}`, "DELETE"); visaForsaljningar(); }
    catch (fel) { alert(fel.message); }
}

document.getElementById("logga-ut").addEventListener("click", async () => {
    await api("/api/auth/logout", "POST");
    location.href = "index.html";
});

document.getElementById("radera").addEventListener("click", async () => {
    if (!confirm("Radera ditt konto? Det går inte att ångra.")) return;
    try { await api("/api/auth/me", "DELETE"); location.href = "index.html"; }
    catch (fel) { alert(fel.message); }
});

start();
