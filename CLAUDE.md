# Casca Print Agent

Servicio Windows independiente (.NET 10, `net10.0-windows`, WinForms/System.Drawing) que imprime tickets en impresoras locales para el POS de Casca. Extraído del repositorio `Casca/`.

- Reglas compartidas del workspace: `../CLAUDE.md`.
- No introducir comentarios en el código (regla en `../CLAUDE.md`), incluida la documentación XML (`///`), salvo que la tarea lo pida.
- Escucha solo en `http://localhost:9001` (no es accesible desde otras máquinas), CORS abierto sin credenciales y sin autenticación: tratar como riesgo conocido.
- Endpoints: `GET /printers`, `GET /printers/{name}/status` (+ WebSocket de estado), `POST /printers/{name}/test`, `POST /tickets/sale`, `POST /labels/product` (etiquetas ZPL de producto, `Services/ProductLabelZplBuilder.cs`, máximo 100 copias).
- Impresión vía driver de Windows (`System.Drawing.Printing`), papel 80 mm. El ticket se dibuja en `Services/SaleTicketRenderer.cs`.
- No usa base de datos ni referencia a otros proyectos de Casca.
- El frontend (`casca-web`) lo consume desde `src/app/features/printers` (configuración de impresora) y el modal de venta exitosa del POS. Cambios de contrato requieren actualizar `printer-agent.models.ts` allí.
- Los preflight responden `Access-Control-Allow-Private-Network` (Chrome, red local).

## Ejecución

- Desarrollo: `dotnet run --project src/Casca.PrintAgent --no-launch-profile` (con consola).
- Release: sin consola; corre en segundo plano con ícono en la bandeja (menú «Salir»). Solo se permite una instancia (mutex `Global\CascaPrintAgent`).

## Instalador

- `.\build-installer.ps1 [-Version 1.0.0]` publica el agente (win-x64, autocontenido) y compila `installer/CascaPrintAgent.iss` con Inno Setup 6 (`winget install JRSoftware.InnoSetup`). Salida: `artifacts/CascaPrintAgent-Setup-<versión>.exe` (ignorada por git).
- Instalación por usuario (sin administrador) en `%LocalAppData%\Programs\Casca Print Agent`, con inicio automático al abrir sesión (clave `HKCU\...\Run`).
- El ícono (`src/Casca.PrintAgent/Assets/casca.ico`) reproduce el de la app de Casca Tiendas.
