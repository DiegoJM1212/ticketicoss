document.addEventListener("DOMContentLoaded", () => {
    const bloquesContainer = document.getElementById("bloques");
    const asientosContainer = document.getElementById("asientos");

    if (!bloquesContainer || !asientosContainer) {
        console.warn("Contenedores de bloques o asientos no encontrados.");
        return;
    }

    bloquesContainer.querySelectorAll(".bloque").forEach(bloque => {
        bloque.addEventListener("click", () => {
            const bloqueId = bloque.dataset.id;

            // Resalta el bloque seleccionado
            bloquesContainer.querySelectorAll(".bloque").forEach(b => b.classList.remove("activo"));
            bloque.classList.add("activo");

            // Construir la URL incluyendo eventId y bloqueId
            const url = `/ComprarBoletos/${eventId}?handler=CargarAsientos&bloqueId=${bloqueId}&eventId=${eventId}`;
            console.log("Fetching URL:", url);

            // Obtener asientos disponibles del backend
            fetch(url)
                .then(response => response.json())
                .then(asientos => {
                    renderAsientos(asientos, bloqueId);
                    actualizarResumen();
                })
                .catch(error => console.error("Error fetching asientos:", error));
        });
    });

    function renderAsientos(asientosDisponibles, bloqueId) {
        if (!Array.isArray(asientosDisponibles)) {
            console.error("La respuesta no es un arreglo:", asientosDisponibles);
            // Opcional: mostrar un mensaje de error al usuario
            asientosContainer.innerHTML = "<p>Error al cargar los asientos. Intente más tarde.</p>";
            return;
        }

        let html = "";
        asientosDisponibles.forEach(asiento => {
            html += `
            <div class="asiento" data-numero="${asiento.numeroAsiento}" data-bloque="${bloqueId}">
                ${asiento.numeroAsiento}
            </div>
        `;
        });
        asientosContainer.innerHTML = html;
        document.querySelectorAll(".asiento").forEach(asiento => {
            asiento.addEventListener("click", () => {
                asiento.classList.toggle("seleccionado");
                actualizarResumen();
            });
        });
    }

    function actualizarResumen() {
        const seleccionados = document.querySelectorAll(".asiento.seleccionado");
        if (seleccionados.length === 0) {
            document.getElementById("subtotal").textContent = "0";
            document.getElementById("impuesto").textContent = "0";
            document.getElementById("servicio").textContent = "0";
            document.getElementById("total").textContent = "0";
            return;
        }

        const precio = parseFloat(document.querySelector(".bloque.activo").dataset.precio);
        const cantidad = seleccionados.length;
        const subtotal = precio * cantidad;
        const impuesto = subtotal * 0.13;
        const servicio = subtotal * 0.03;
        const total = subtotal + impuesto + servicio;

        document.getElementById("subtotal").textContent = subtotal.toFixed(2);
        document.getElementById("impuesto").textContent = impuesto.toFixed(2);
        document.getElementById("servicio").textContent = servicio.toFixed(2);
        document.getElementById("total").textContent = total.toFixed(2);
    }
});
