// Contact page: checks the form, sends it to /api/contact and shows a thank-you message.
const form = document.getElementById("kontakt-form");
const felruta = document.getElementById("kontakt-fel");
const meddelande = document.getElementById("meddelande");

function visaFel(text) {
    felruta.textContent = text;
    felruta.hidden = false;
}

// Live character counter under the message box.
meddelande.addEventListener("input", () => {
    document.getElementById("antal").textContent = meddelande.value.length;
});

form.addEventListener("submit", async (e) => {
    e.preventDefault(); // stop the browser from reloading the page
    felruta.hidden = true;

    const data = {
        firstName: document.getElementById("fornamn").value.trim(),
        lastName: document.getElementById("efternamn").value.trim(),
        email: document.getElementById("epost").value.trim(),
        topic: document.getElementById("arende").value,
        message: meddelande.value.trim(),
        website: document.getElementById("webbplats").value,
    };

    // Check in the browser first, so the user gets a clear message straight away.
    // The server checks the same things again, since anyone can skip the browser.
    if (!data.firstName || !data.lastName) return visaFel("Fyll i både förnamn och efternamn.");
    if (!document.getElementById("epost").checkValidity() || !data.email) return visaFel("Skriv en giltig e-postadress.");
    if (!data.topic) return visaFel("Välj vad ditt ärende gäller.");
    if (data.message.length < 10) return visaFel("Skriv lite mer i meddelandet (minst 10 tecken).");

    const knapp = document.getElementById("skicka");
    knapp.disabled = true;
    knapp.textContent = "Skickar…";
    try {
        await api("/api/contact", "POST", data);
        document.getElementById("tack-epost").textContent = data.email;
        form.hidden = true;
        document.getElementById("kontakt-tack").hidden = false;
    } catch (fel) {
        visaFel(fel.message);
    } finally {
        knapp.disabled = false;
        knapp.textContent = "Skicka meddelande";
    }
});

// Logged in? Fill in name and email automatically, and show "Mina sidor" instead of "Logga in".
api("/api/auth/me").then(jag => {
    const [fornamn, ...efternamn] = (jag.fullName ?? "").split(" ");
    document.getElementById("fornamn").value ||= fornamn;
    document.getElementById("efternamn").value ||= efternamn.join(" ");
    document.getElementById("epost").value ||= jag.email;
    const knapp = document.getElementById("login-knapp");
    knapp.textContent = "Mina sidor";
    knapp.href = "mina-sidor.html";
}).catch(() => {});
