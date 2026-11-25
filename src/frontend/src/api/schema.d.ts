// Arquivo gerado por `npm run api:tipos` a partir de /openapi/v1.json. Não editar.
export interface paths {
    "/health": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Indica se a API está em execução. */
        get: operations["ConsultarSaude"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/autenticacao/registrar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Cadastra um usuário PF ou PJ e cria sua carteira. */
        post: operations["Registrar"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/autenticacao/login": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Valida as credenciais e emite o token de acesso (JWT). */
        post: operations["Login"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/carteiras/minha": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Retorna a carteira e o saldo atual do usuário autenticado. */
        get: operations["ObterMinhaCarteira"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/usuarios/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Retorna os dados cadastrais do usuário autenticado. */
        get: operations["ObterTitular"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Transfere da carteira do usuário (PF) para uma conta PF ou PJ, com idempotência.
         * @description Reenvios com o mesmo cabeçalho Idempotency-Key devolvem 200 com o comprovante original, sem novo débito. Valor acima do limite por operação ou do limite diário (dia de Brasília) recebe 422 limite-excedido, com tipoLimite, limite e valorDisponivel.
         */
        post: operations["Transferir"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/destinatarios/consultar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Confirma o nome e o tipo de conta do destinatário pelo documento, antes de transferir. */
        post: operations["ConsultarDestinatario"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/extrato": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Lista os lançamentos da carteira do usuário (PF e PJ), do mais recente para o mais antigo. */
        get: operations["ObterExtrato"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/limites": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Limites de transferência da PF e quanto ainda pode ser enviado hoje (dia de Brasília).
         * @description Informativo: o envio verifica os limites de novo. Limites e disponível nulos = sem limite.
         */
        get: operations["ConsultarLimites"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/{transferenciaId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Reabre o comprovante de uma transferência (pagador ou recebedor).
         * @description Para o recebedor, chaveIdempotencia vem nula. Transferência inexistente ou de que o usuário não participa recebe o mesmo 404.
         */
        get: operations["ConsultarTransferencia"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cartoes": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Solicita o cartão de crédito escolhendo a bandeira (001 Visa, 002 Mastercard).
         * @description Cada CPF tem no máximo um cartão e a bandeira não muda depois.
         */
        post: operations["SolicitarCartao"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cartoes/meu": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Cartão do usuário, com limites; sem número completo nem CVV. */
        get: operations["ObterMeuCartao"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cartoes/meu/dados": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Nome, número, validade e CVV do próprio cartão, para exibição ao titular.
         * @description Os dados não são gravados nem guardados em cache (Cache-Control: no-store).
         */
        get: operations["ExibirDadosDoCartao"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Emite uma cobrança na carteira do lojista (PJ). */
        post: operations["EmitirCobranca"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas/minhas": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Cobranças emitidas pelo lojista, das mais novas para as mais antigas. */
        get: operations["ListarMinhasCobrancas"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas/codigo/{codigo}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Dados de uma cobrança pelo código curto do link (XXXX-XXXX).
         * @description Aceita minúsculas, espaços e o código sem hífen. Mesma resposta e visibilidade da consulta pelo identificador; código malformado ou inexistente = 404.
         */
        get: operations["ConsultarCobrancaPorCodigo"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas/{cobrancaId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Dados de uma cobrança, para quem vai pagar ou para o emissor.
         * @description pagamento só aparece para o emissor e o pagador; ultimaRecusa, só para o emissor.
         */
        get: operations["ConsultarCobranca"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas/{cobrancaId}/cancelar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Cancela uma cobrança pendente do próprio lojista. */
        post: operations["CancelarCobranca"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/cobrancas/{cobrancaId}/pagar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Paga uma cobrança com o cartão One Bank do próprio pagador (PF), com idempotência.
         * @description A bandeira simulada recusa com 422: cartao-nao-autorizado, cartao-bloqueado-fatura-vencida ou limite-cartao-insuficiente. A recusa mantém a cobrança pendente e não consome a chave.
         */
        post: operations["PagarCobranca"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/faturas": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Faturas do cartão, da mais nova para a mais antiga, com a do ciclo atual. */
        get: operations["ListarFaturas"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/faturas/{competencia}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Fatura do mês (AAAA-MM), com os lançamentos.
         * @description valorAPagar traz o total com os juros de hoje em faturas disponíveis ou vencidas.
         */
        get: operations["ObterFatura"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/faturas/{competencia}/pagar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Paga o total da fatura com o saldo da carteira, com idempotência.
         * @description Fatura vencida inclui juros por dia de atraso. O limite usado pelas compras volta a ficar disponível.
         */
        post: operations["PagarFatura"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/recebiveis": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Recebíveis do lojista, dos mais novos para os mais antigos. */
        get: operations["ListarRecebiveis"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/recebiveis/{recebivelId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Um recebível do lojista. */
        get: operations["ObterRecebivel"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/recebiveis/{recebivelId}/antecipar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Antecipa um recebível com taxa de 2% e crédito imediato, com idempotência. */
        post: operations["AnteciparRecebivel"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        BancoResposta: {
            codigo: string;
            nome: string;
        };
        BandeiraResposta: {
            codigo: string;
            nome: string;
        };
        CartaoResposta: {
            /** Format: uuid */
            cartaoId: string;
            bandeira: components["schemas"]["BandeiraResposta"];
            nomeImpresso: string;
            ultimosDigitos: string;
            validade: string;
            /** Format: double */
            limiteTotal: number | string;
            /** Format: double */
            limiteDisponivel: number | string;
            /** Format: double */
            comprasALancar: number | string;
            /** Format: date-time */
            criadoEm: string;
        };
        CobrancaResposta: {
            /** Format: uuid */
            cobrancaId: string;
            codigo: string;
            lojista: components["schemas"]["LojistaResposta"];
            /** Format: double */
            valor: number | string;
            descricao: string;
            status: string;
            /** Format: date-time */
            criadoEm: string;
            /** Format: date-time */
            pagaEm: null | string;
            /** Format: date-time */
            canceladaEm: null | string;
            pagamento: null | components["schemas"]["PagamentoDaCobrancaResposta"];
            ultimaRecusa: null | components["schemas"]["UltimaRecusaResposta"];
        };
        ComprovanteResposta: {
            /** Format: uuid */
            transferenciaId: string;
            /** Format: uuid */
            chaveIdempotencia: null | string;
            pagador: components["schemas"]["PagadorResposta"];
            recebedor: components["schemas"]["RecebedorResposta"];
            /** Format: double */
            valor: number | string;
            descricao: null | string;
            status: string;
            /** Format: date-time */
            criadoEm: string;
        };
        ConsultaDestinatarioRequisicao: {
            documento: null | string;
        };
        ContaBancariaResposta: {
            banco: components["schemas"]["BancoResposta"];
            agencia: string;
            numero: string;
            digito: string;
            tipo: string;
        };
        ContraparteResposta: {
            nome: string;
            documento: string;
            tipoConta: string;
        };
        DadosCartaoResposta: {
            nomeImpresso: string;
            numero: string;
            validade: string;
            cvv: string;
            bandeira: components["schemas"]["BandeiraResposta"];
        };
        DestinatarioResposta: {
            nome: string;
            tipoConta: string;
        };
        EmitirCobrancaRequisicao: {
            /** Format: double */
            valor: null | number | string;
            descricao: null | string;
        };
        ExtratoResposta: {
            itens: components["schemas"]["ItemExtratoResposta"][];
            /** Format: int32 */
            paginaAtual: number | string;
            /** Format: int32 */
            tamanhoPagina: number | string;
            /** Format: int32 */
            totalItens: number | string;
        };
        FaturaDetalhadaResposta: {
            competencia: string;
            /** Format: date */
            inicio: string;
            /** Format: date */
            fechamento: string;
            /** Format: date */
            vencimento: string;
            situacao: string;
            /** Format: double */
            total: number | string;
            /** Format: int32 */
            diasAtraso: number | string;
            /** Format: double */
            juros: number | string;
            /** Format: double */
            valorAPagar: null | number | string;
            /** Format: date-time */
            pagaEm: null | string;
            /** Format: double */
            valorPago: null | number | string;
            lancamentos: components["schemas"]["LancamentoFaturaResposta"][];
        };
        FaturaResposta: {
            competencia: string;
            /** Format: date */
            inicio: string;
            /** Format: date */
            fechamento: string;
            /** Format: date */
            vencimento: string;
            situacao: string;
            /** Format: double */
            total: number | string;
            /** Format: int32 */
            diasAtraso: number | string;
            /** Format: double */
            juros: number | string;
            /** Format: double */
            valorAPagar: null | number | string;
            /** Format: date-time */
            pagaEm: null | string;
            /** Format: double */
            valorPago: null | number | string;
        };
        HttpValidationProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            detail?: null | string;
            instance?: null | string;
            errors?: {
                [key: string]: string[];
            };
        };
        ItemExtratoResposta: {
            /** Format: uuid */
            lancamentoId: string;
            tipo: string;
            direcao: string;
            /** Format: uuid */
            transferenciaId: null | string;
            contraparte: null | components["schemas"]["ContraparteResposta"];
            /** Format: double */
            valor: number | string;
            descricao: null | string;
            /** Format: date-time */
            criadoEm: string;
        };
        LancamentoFaturaResposta: {
            estabelecimento: string;
            descricao: string;
            /** Format: double */
            valor: number | string;
            /** Format: date */
            dataCompra: string;
        };
        LimitesResposta: {
            /** Format: double */
            porOperacao: null | number | string;
            /** Format: double */
            diario: null | number | string;
            /** Format: double */
            transferidoHoje: number | string;
            /** Format: double */
            disponivelHoje: null | number | string;
            /** Format: date */
            dia: string;
        };
        LoginRequisicao: {
            email: null | string;
            senha: null | string;
        };
        LoginResposta: {
            tokenAcesso: string;
            tipoToken: string;
            /** Format: int32 */
            expiraEmSegundos: number | string;
        };
        LojistaResposta: {
            /** Format: uuid */
            usuarioId: string;
            nome: string;
            documento: string;
        };
        MinhaCarteiraResposta: {
            /** Format: uuid */
            carteiraId: string;
            /** Format: uuid */
            usuarioId: string;
            nomeTitular: string;
            tipoConta: string;
            /** Format: double */
            saldo: number | string;
            moeda: string;
            /** Format: date-time */
            atualizadoEm: string;
            contaBancaria: components["schemas"]["ContaBancariaResposta"];
        };
        PagadorResposta: {
            /** Format: uuid */
            usuarioId: string;
            nome: string;
            documento: string;
        };
        PagamentoDaCobrancaResposta: {
            bandeira: components["schemas"]["BandeiraResposta"];
            ultimosDigitosCartao: string;
            codigoAutorizacao: string;
        };
        PagamentoResposta: {
            /** Format: uuid */
            cobrancaId: string;
            status: string;
            /** Format: double */
            valor: number | string;
            bandeira: components["schemas"]["BandeiraResposta"];
            ultimosDigitosCartao: string;
            codigoAutorizacao: string;
            lojistaNome: string;
            /** Format: date-time */
            pagaEm: string;
        };
        PagarCobrancaRequisicao: {
            nomeTitularCartao: null | string;
            numeroCartao: null | string;
            validade: null | string;
            cvv: null | string;
        };
        PaginaCobrancasResposta: {
            itens: components["schemas"]["CobrancaResposta"][];
            /** Format: int32 */
            paginaAtual: number | string;
            /** Format: int32 */
            tamanhoPagina: number | string;
            /** Format: int32 */
            totalItens: number | string;
        };
        PaginaFaturasResposta: {
            itens: components["schemas"]["FaturaResposta"][];
            /** Format: int32 */
            paginaAtual: number | string;
            /** Format: int32 */
            tamanhoPagina: number | string;
            /** Format: int32 */
            totalItens: number | string;
        };
        PaginaRecebiveisResposta: {
            itens: components["schemas"]["RecebivelResposta"][];
            /** Format: int32 */
            paginaAtual: number | string;
            /** Format: int32 */
            tamanhoPagina: number | string;
            /** Format: int32 */
            totalItens: number | string;
        };
        ProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            detail?: null | string;
            instance?: null | string;
        };
        RecebedorResposta: {
            /** Format: uuid */
            usuarioId: string;
            nome: string;
            documento: string;
            tipoConta: string;
        };
        RecebivelResposta: {
            /** Format: uuid */
            recebivelId: string;
            /** Format: uuid */
            cobrancaId: string;
            descricao: string;
            situacao: string;
            /** Format: double */
            valorBruto: number | string;
            /** Format: double */
            taxa: number | string;
            /** Format: double */
            valorLiquido: number | string;
            /** Format: date */
            dataPrevista: string;
            /** Format: date-time */
            recebidoEm: null | string;
            /** Format: date-time */
            criadoEm: string;
        };
        RegistrarRequisicao: {
            nomeCompleto: null | string;
            email: null | string;
            documento: null | string;
            tipoConta: null | string;
            senha: null | string;
        };
        SolicitarCartaoRequisicao: {
            bandeira: null | string;
        };
        TitularResposta: {
            /** Format: uuid */
            usuarioId: string;
            nomeCompleto: string;
            email: string;
            documento: string;
            tipoConta: string;
            /** Format: date-time */
            criadoEm: string;
        };
        TransferenciaRequisicao: {
            documentoDestino: null | string;
            /** Format: double */
            valor: null | number | string;
            descricao: null | string;
        };
        UltimaRecusaResposta: {
            motivo: string;
            /** Format: date-time */
            em: string;
        };
        UsuarioRegistradoResposta: {
            /** Format: uuid */
            usuarioId: string;
            /** Format: uuid */
            carteiraId: string;
            nomeCompleto: string;
            email: string;
            documento: string;
            tipoConta: string;
            /** Format: double */
            saldoInicial: number | string;
            moeda: string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    ConsultarSaude: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "text/plain": string;
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "text/plain": string;
                };
            };
        };
    };
    Registrar: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["RegistrarRequisicao"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["UsuarioRegistradoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    Login: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["LoginRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["LoginResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterMinhaCarteira: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["MinhaCarteiraResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterTitular: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TitularResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    Transferir: {
        parameters: {
            query?: never;
            header: {
                /** @description UUID gerado pelo cliente; reenvios com a mesma chave não debitam de novo. */
                "Idempotency-Key": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["TransferenciaRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarDestinatario: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ConsultaDestinatarioRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DestinatarioResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterExtrato: {
        parameters: {
            query?: {
                pagina?: string;
                tamanhoPagina?: string;
                direcao?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ExtratoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarLimites: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["LimitesResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarTransferencia: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                transferenciaId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    SolicitarCartao: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["SolicitarCartaoRequisicao"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CartaoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterMeuCartao: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CartaoResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ExibirDadosDoCartao: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DadosCartaoResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    EmitirCobranca: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["EmitirCobrancaRequisicao"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CobrancaResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListarMinhasCobrancas: {
        parameters: {
            query?: {
                status?: string;
                pagina?: string;
                tamanhoPagina?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PaginaCobrancasResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarCobrancaPorCodigo: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                codigo: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CobrancaResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarCobranca: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                cobrancaId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CobrancaResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CancelarCobranca: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                cobrancaId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CobrancaResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    PagarCobranca: {
        parameters: {
            query?: never;
            header: {
                /** @description UUID gerado pelo cliente; reenvios com a mesma chave devolvem o pagamento original. */
                "Idempotency-Key": string;
            };
            path: {
                cobrancaId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["PagarCobrancaRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PagamentoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListarFaturas: {
        parameters: {
            query?: {
                pagina?: string;
                tamanhoPagina?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PaginaFaturasResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterFatura: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                competencia: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["FaturaDetalhadaResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    PagarFatura: {
        parameters: {
            query?: never;
            header: {
                /** @description UUID gerado pelo cliente; reenvios com a mesma chave não debitam de novo. */
                "Idempotency-Key": string;
            };
            path: {
                competencia: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["FaturaDetalhadaResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListarRecebiveis: {
        parameters: {
            query?: {
                situacao?: string;
                pagina?: string;
                tamanhoPagina?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["PaginaRecebiveisResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterRecebivel: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                recebivelId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RecebivelResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    AnteciparRecebivel: {
        parameters: {
            query?: never;
            header: {
                /** @description UUID gerado pelo cliente; reenvios com a mesma chave não creditam de novo. */
                "Idempotency-Key": string;
            };
            path: {
                recebivelId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["RecebivelResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
}
