-- Crear la base de datos
CREATE DATABASE SistemaVentaBoletos;
GO

USE SistemaVentaBoletos;
GO

-- Tabla de Usuarios
CREATE TABLE Usuarios (
    id_usuario INT PRIMARY KEY IDENTITY(1,1),
    nombre VARCHAR(100),
    apellidos VARCHAR(100),
    correo VARCHAR(150) UNIQUE,
    contrasena VARCHAR(255),
    identificacion VARCHAR(20) UNIQUE,
    telefono VARCHAR(20),
    tipo_usuario VARCHAR(20) CHECK (tipo_usuario IN ('cliente', 'empleado', 'admin')) DEFAULT 'cliente',
    estado VARCHAR(20) CHECK (estado IN ('activo', 'inactivo')) DEFAULT 'activo',
    fecha_registro DATE,
    ultima_fecha_acceso DATETIME,
    doble_factor_habilitado BIT DEFAULT 0
);

-- Tabla de Autenticación
CREATE TABLE Autenticacion (
    id INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    codigo_2FA VARCHAR(6),
    codigo_recuperacion VARCHAR(10),
    fecha_generacion DATETIME,
    fecha_expiracion DATETIME,
    utilizado BIT DEFAULT 0,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
);

-- Tabla de Eventos
CREATE TABLE Eventos (
    id_evento INT PRIMARY KEY IDENTITY(1,1),
    nombre VARCHAR(255),
    descripcion TEXT,
    fecha DATE,
    lugar VARCHAR(255),
    estado VARCHAR(20) CHECK (estado IN ('activo', 'cancelado', 'finalizado')) DEFAULT 'activo',
    imagen_url VARCHAR(255)
);

-- Tabla de Boletos
CREATE TABLE Boletos (
    id_boleto INT PRIMARY KEY IDENTITY(1,1),
    id_evento INT,
    categoria VARCHAR(20) CHECK (categoria IN ('VIP', 'General', 'Preferencial')),
    precio DECIMAL(10,2),
    cantidad_disponible INT,
    limite_compra_por_usuario INT,
    id_preventa INT,
    precio_preventa DECIMAL(10,2),
    cantidad_preventa INT,
    FOREIGN KEY (id_evento) REFERENCES Eventos(id_evento),
    FOREIGN KEY (id_preventa) REFERENCES Preventa(id_preventa)
);

-- Tabla de Compras
CREATE TABLE Compras (
    id_compra INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    total DECIMAL(10,2),
    estado VARCHAR(20) CHECK (estado IN ('pendiente', 'pagado', 'cancelado')) DEFAULT 'pendiente',
    fecha DATE,
    numero_factura VARCHAR(50),
    id_codigo_promocional INT,
    descuento_aplicado DECIMAL(10,2),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario),
    FOREIGN KEY (id_codigo_promocional) REFERENCES CodigosPromocionales(id_codigo)
);

-- Tabla de Detalle de Compra
CREATE TABLE DetalleCompra (
    id_detalle INT PRIMARY KEY IDENTITY(1,1),
    id_compra INT,
    id_boleto INT,
    cantidad INT,
    subtotal DECIMAL(10,2),
    FOREIGN KEY (id_compra) REFERENCES Compras(id_compra),
    FOREIGN KEY (id_boleto) REFERENCES Boletos(id_boleto)
);

-- Tabla de Pagos de Entradas
CREATE TABLE PagosEntradas (
    id_pago INT PRIMARY KEY IDENTITY(1,1),
    id_compra INT,
    metodo_pago VARCHAR(20) CHECK (metodo_pago IN ('Tarjeta Crédito', 'Tarjeta Débito', 'PayPal')),
    numero_tarjeta VARCHAR(20),
    vencimiento VARCHAR(7),
    cvv VARCHAR(4),
    estado VARCHAR(20) CHECK (estado IN ('aprobado', 'rechazado')) DEFAULT 'aprobado',
    id_codigo_promocional INT,
    descuento_aplicado DECIMAL(10,2),
    FOREIGN KEY (id_compra) REFERENCES Compras(id_compra),
    FOREIGN KEY (id_codigo_promocional) REFERENCES CodigosPromocionales(id_codigo)
);

-- Tabla de Pagos de Códigos Promocionales
CREATE TABLE PagosCodigosPromocionales (
    id_pago_codigo INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    id_codigo_promocional INT,
    monto DECIMAL(10,2),
    numero_tarjeta VARCHAR(20),
    vencimiento VARCHAR(7),
    cvv VARCHAR(4),
    estado VARCHAR(20) CHECK (estado IN ('aprobado', 'rechazado', 'pendiente')) DEFAULT 'pendiente',
    fecha_pago DATETIME,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario),
    FOREIGN KEY (id_codigo_promocional) REFERENCES CodigosPromocionales(id_codigo)
);

-- Tabla de Soporte
CREATE TABLE Soporte (
    id_ticket INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    mensaje TEXT,
    estado VARCHAR(20) CHECK (estado IN ('abierto', 'en proceso', 'cerrado')) DEFAULT 'abierto',
    fecha DATE,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
);

-- Tabla de Códigos Promocionales
CREATE TABLE CodigosPromocionales (
    id_codigo INT PRIMARY KEY IDENTITY(1,1),
    codigo VARCHAR(20) UNIQUE,
    descuento DECIMAL(5,2),
    fecha_expiracion DATE,
    estado VARCHAR(20) CHECK (estado IN ('activo', 'expirado')) DEFAULT 'activo'
);

-- Tabla de Roles
CREATE TABLE Roles (
    id_rol INT PRIMARY KEY IDENTITY(1,1),
    nombre_rol VARCHAR(50),
    descripcion TEXT
);

-- Tabla de Permisos
CREATE TABLE Permisos (
    id_permiso INT PRIMARY KEY IDENTITY(1,1),
    nombre_permiso VARCHAR(100),
    descripcion TEXT
);

-- Tabla de Relación Roles-Permisos
CREATE TABLE RolesPermisos (
    id_rol INT,
    id_permiso INT,
    PRIMARY KEY (id_rol, id_permiso),
    FOREIGN KEY (id_rol) REFERENCES Roles(id_rol),
    FOREIGN KEY (id_permiso) REFERENCES Permisos(id_permiso)
);

-- Tabla de Relación Usuarios-Roles
CREATE TABLE UsuariosRoles (
    id_usuario INT,
    id_rol INT,
    PRIMARY KEY (id_usuario, id_rol),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario),
    FOREIGN KEY (id_rol) REFERENCES Roles(id_rol)
);

-- Tabla de Historial de Cambios de Contraseña
CREATE TABLE HistorialCambiosContrasena (
    id INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    fecha_cambio DATETIME,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
);

-- Tabla de Registro de Autenticación
CREATE TABLE RegistroAutenticacion (
    id INT PRIMARY KEY IDENTITY(1,1),
    id_usuario INT,
    fecha_intento DATETIME,
    tipo_autenticacion VARCHAR(20) CHECK (tipo_autenticacion IN ('password', '2FA')),
    exito BIT,
    direccion_ip VARCHAR(50),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
);

-- Tabla de Preventa
CREATE TABLE Preventa (
    id_preventa INT PRIMARY KEY IDENTITY(1,1),
    id_evento INT,
    fecha_inicio DATETIME,
    fecha_fin DATETIME,
    estado VARCHAR(20) CHECK (estado IN ('activa', 'finalizada', 'cancelada')) DEFAULT 'activa',
    FOREIGN KEY (id_evento) REFERENCES Eventos(id_evento)
);

-- Insertar eventos en preventa
INSERT INTO Eventos (nombre, descripcion, fecha, lugar, estado, imagen_url)
VALUES 
('Concierto de red hot chilli peppers', 'Un concierto épico de rock con bandas internacionales.', '2025-04-20', 'Estadio Nacional', 'activo', 'images/concierto1.png'),
('saprissa vs la liga', 'El partido más esperado de la temporada.', '2025-05-01', 'Estadio Azteca', 'activo', 'images/partido1.jpg');

-- Insertar preventas
INSERT INTO Preventa (id_evento, fecha_inicio, fecha_fin, estado)
VALUES 
(1, '2025-04-01', '2025-04-19', 'activa'),
(2, '2025-04-15', '2025-04-30', 'activa');

-- Insertar códigos promocionales
INSERT INTO CodigosPromocionales (codigo, descuento, fecha_expiracion, estado)
VALUES
('DESCUENTO10', 3000.00, '2025-06-30', 'activo'),
('2x1', 5000.00, '2025-07-15', 'activo');


-- Insertar preguntas frecuentes
INSERT INTO PreguntasFrecuentes (pregunta, respuesta, fecha)
VALUES
('¿Cómo puedo comprar boletos?', 'Puedes comprar boletos en nuestra página web, seleccionando el evento y método de pago.', '2025-03-15'),
('¿Cómo puedo usar un código promocional?', 'Debes ingresar el código promocional en el campo correspondiente antes de finalizar la compra.', '2025-03-14');


-- Insertar tickets de atención al cliente
INSERT INTO Soporte (id_usuario, mensaje, estado, fecha)
VALUES 
(1, 'No puedo completar mi compra, ¿qué hago?', 'abierto', '2025-03-16'),
(3, '¿Puedo cambiar la fecha de mi entrada?', 'en proceso', '2025-03-15');

-- Insertar eventos destacados
INSERT INTO Eventos (nombre, descripcion, fecha, lugar, estado, imagen_url)
VALUES 
('Concierto de red hot chilli peppers', 'El evento más importante del año.', '2025-12-31', 'Auditorio Nacional', 'activo', 'images/concierto1.png'),
('saprissa vs la liga', 'Equipos de todo el mundo compiten.', '2025-07-10', 'Estadio Metropolitano', 'activo', 'images/partido1.jpg');


ALTER TABLE CodigosPromocionales
ALTER COLUMN descuento DECIMAL(10,2);

ALTER TABLE Soporte
ADD respuesta TEXT NULL;


CREATE TABLE PreguntasFrecuentes (
    id_pregunta INT PRIMARY KEY IDENTITY(1,1),
    pregunta TEXT,
    respuesta TEXT,
    fecha DATE
);




SELECT * FROM Usuarios;

UPDATE Usuarios
SET doble_factor_habilitado = 1
WHERE id_usuario = 1;

SELECT id_usuario, nombre, correo, doble_factor_habilitado 
FROM Usuarios
WHERE id_usuario = 1;



-- Insertar roles en la tabla Roles existente
INSERT INTO Roles (nombre_rol, descripcion)
VALUES 
('gestor_eventos', 'Usuario que puede gestionar eventos'),
('gestor_ventas', 'Usuario que puede gestionar ventas'),
('patrocinador', 'Usuario patrocinador de eventos'),
('manager', 'Usuario con acceso completo al sistema');

-- Insertar permisos básicos
INSERT INTO Permisos (nombre_permiso, descripcion)
VALUES 
('comprar_boletos', 'Permiso para comprar boletos'),
('gestionar_eventos', 'Permiso para gestionar eventos'),
('gestionar_ventas', 'Permiso para gestionar ventas'),
('gestionar_usuarios', 'Permiso para gestionar usuarios'),
('gestionar_codigos', 'Permiso para gestionar códigos promocionales');

-- Asignar permisos a roles
-- Gestor de eventos
INSERT INTO RolesPermisos (id_rol, id_permiso)
VALUES 
((SELECT id_rol FROM Roles WHERE nombre_rol = 'gestor_eventos'), 
 (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_eventos'));

-- Gestor de ventas
INSERT INTO RolesPermisos (id_rol, id_permiso)
VALUES 
((SELECT id_rol FROM Roles WHERE nombre_rol = 'gestor_ventas'), 
 (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_ventas'));

-- Manager (todos los permisos)
DECLARE @manager_id INT = (SELECT id_rol FROM Roles WHERE nombre_rol = 'manager');
INSERT INTO RolesPermisos (id_rol, id_permiso)
VALUES 
(@manager_id, (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'comprar_boletos')),
(@manager_id, (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_eventos')),
(@manager_id, (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_ventas')),
(@manager_id, (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_usuarios')),
(@manager_id, (SELECT id_permiso FROM Permisos WHERE nombre_permiso = 'gestionar_codigos'));

-- Para probar, asignar un rol a un usuario (ajusta el id_usuario según necesites)
-- Por ejemplo, para asignar el rol de manager al usuario con id 2:
INSERT INTO UsuariosRoles (id_usuario, id_rol)
VALUES (2, (SELECT id_rol FROM Roles WHERE nombre_rol = 'manager'));

-- Actualizar el tipo_usuario en la tabla Usuarios para reflejar los nuevos roles
-- Esto es necesario porque tu código actual usa el campo tipo_usuario para la redirección
UPDATE Usuarios
SET tipo_usuario = 'admin'
WHERE id_usuario IN (
    SELECT id_usuario FROM UsuariosRoles 
    WHERE id_rol IN (
        SELECT id_rol FROM Roles 
        WHERE nombre_rol IN ('gestor_eventos', 'gestor_ventas', 'patrocinador', 'manager')
    )
);




-- Crear la tabla TarjetasUsuario si no existe
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TarjetasUsuario')
BEGIN
    CREATE TABLE TarjetasUsuario (
        IdTarjeta INT PRIMARY KEY IDENTITY(1,1),
        IdUsuario INT NOT NULL,
        TipoTarjeta VARCHAR(50) NOT NULL,
        NumeroTarjeta VARCHAR(20) NOT NULL,
        Vencimiento VARCHAR(7) NOT NULL,
        CVV VARCHAR(4) NOT NULL,
        FOREIGN KEY (IdUsuario) REFERENCES Usuarios(id_usuario)
    );
END

