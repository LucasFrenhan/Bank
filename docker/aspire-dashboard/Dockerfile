# Imagem derivada do Aspire Dashboard apenas para permitir healthcheck.
#
# A imagem oficial é distroless (sem sh, curl ou wget), o que impede qualquer healthcheck
# — exigido pela constituição, FR-005 e SC-003. Aqui só é acrescentado o binário estático
# do BusyBox, usado como `busybox wget`. Nada mais muda na imagem oficial.
# Ver specs/001-infra-local/research.md (R3) e plan.md (Complexity Tracking).

FROM busybox:1.37.0-musl AS busybox

FROM mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5
COPY --from=busybox /bin/busybox /usr/local/bin/busybox
