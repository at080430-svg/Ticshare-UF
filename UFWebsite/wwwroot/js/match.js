const matchId = new URLSearchParams(location.search).get("id");

async function visaMatch() {
    const m = await api(`/api/matches/${matchId}`);
    const tid = new Date(m.startTime);
    const dag = tid.toLocaleDateString("sv-SE", { weekday: "long", day: "numeric", month: "long" });
    const klockslag = tid.toLocaleTimeString("sv-SE", { hour: "2-digit", minute: "2-digit" });

    document.title = `${m.home} – ${m.away} | Ticshare UF`;
    // The whole page takes the sport's colours (see css/sporter.css).
    document.body.classList.add("sportsida", sportKlass(m.sport));
    document.getElementById("serie").textContent = m.competition ? `${m.sport} · ${m.competition}` : m.sport;
    document.getElementById("rubrik").innerHTML = `${esc(m.home)} <span class="mot">mot</span> ${esc(m.away)}`;
    document.getElementById("info").textContent = `${dag} · ${klockslag} · ${m.arena}${m.city ? `, ${m.city}` : ""}`;

    const kvar = m.tickets.length;
    document.getElementById("kvar").textContent = kvar === 1 ? "1 biljett kvar" : `${kvar} biljetter kvar`;

    const box = document.getElementById("biljetter");
    if (kvar === 0) {
        box.innerHTML = `<p class="tomt">Inga biljetter till salu just nu. Titta in igen senare!</p>`;
        return;
    }
    // One ticket-shaped card per seat, cheapest first (the API already sorts by price).
    box.innerHTML = m.tickets.map((t, i) => `
        <div class="biljett">
            ${i === 0 && kvar > 1 ? `<span class="etikett">Billigast</span>` : ""}
            <div class="platser">
                <div><span>Sektion</span><strong>${esc(t.section)}</strong></div>
                <div><span>Rad</span><strong>${esc(t.row)}</strong></div>
                <div><span>Plats</span><strong>${esc(t.seat)}</strong></div>
            </div>
            <div class="biljett-botten">
                <div>
                    <span class="pris">${kronor(t.price)}</span>
                    ${t.faceValue > t.price ? `<s>${kronor(t.faceValue)}</s>` : ""}
                    <small>+ ${kronor(m.serviceFee)} serviceavgift</small>
                </div>
                <button onclick="kop(${t.id})">Köp</button>
            </div>
        </div>`).join("");
}

async function kop(listingId) {
    try {
        const order = await api("/api/orders", "POST", { listingId });
        document.getElementById("belopp").textContent = kronor(order.totalAmount);
        document.getElementById("swishnummer").textContent = order.swishNumber;
        document.getElementById("referens").textContent = order.reference;
        document.getElementById("deadline").textContent = new Date(order.payBefore).toLocaleTimeString("sv-SE", { hour: "2-digit", minute: "2-digit" });
        document.getElementById("betalning").hidden = false;
        document.getElementById("biljetter").hidden = true;
        document.getElementById("biljetter-rubrik").hidden = true;

        // The simulate endpoint only exists while developing; on the live site this returns 404.
        const knapp = document.getElementById("simulera");
        knapp.hidden = location.hostname !== "localhost";
        knapp.onclick = async () => {
            await api(`/api/orders/${order.id}/simulate-payment`, "POST");
            location.href = "mina-sidor.html";
        };
    } catch (fel) {
        if (fel.message.includes("logga in")) location.href = "login.html";
        else { alert(fel.message); visaMatch(); }
    }
}

visaMatch();
