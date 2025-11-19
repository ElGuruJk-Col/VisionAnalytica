# **Guía de Diseño UI - VisioAnalytica**

## **Filosofía de Diseño**

**Principios:**
- **Suave y agradable:** Colores suaves, transiciones suaves, sin elementos agresivos
- **Práctico:** Cada elemento tiene un propósito claro
- **Moderno:** Inspirado en las mejores prácticas de diseño actual
- **Consistente:** Mismo estilo en toda la aplicación

---

## **Referencias de Inspiración**

### **1. Siigo (Software Contable)**
**Características a adoptar:**
- ✅ **Colores suaves:** Paleta de azules y grises suaves
- ✅ **Espaciado generoso:** Mucho espacio en blanco
- ✅ **Tipografía clara:** Fuentes sans-serif legibles
- ✅ **Iconos minimalistas:** Iconos simples y reconocibles
- ✅ **Navegación clara:** Menús laterales bien organizados
- ✅ **Cards y contenedores:** Información organizada en tarjetas

### **2. Google Fotos**
**Características a adoptar:**
- ✅ **Galería visual:** Grid de imágenes con miniaturas
- ✅ **Gestos intuitivos:** Swipe, pinch to zoom
- ✅ **Transiciones suaves:** Animaciones fluidas
- ✅ **Búsqueda visual:** Fácil encontrar fotos
- ✅ **Vista ampliada:** Experiencia inmersiva al ver fotos
- ✅ **Colores adaptativos:** Material Design

### **3. Odoo CRM**
**Características a adoptar:**
- ✅ **Organización funcional:** Información estructurada
- ✅ **Filtros y búsqueda:** Fácil encontrar información
- ✅ **Estados visuales:** Colores para estados (pendiente, completado, etc.)
- ✅ **Formularios claros:** Campos bien organizados
- ✅ **Dashboard informativo:** Métricas visibles
- ✅ **Acciones rápidas:** Botones de acción prominentes

---

## **Paleta de Colores**

### **Colores Principales**
```css
/* Primario - Azul suave (inspirado en Siigo) */
Primary: #4A90E2
Primary Dark: #357ABD
Primary Light: #6BA3E8

/* Secundario - Verde suave (éxito, completado) */
Success: #52C41A
Success Light: #73D13D

/* Advertencia - Naranja suave */
Warning: #FA8C16
Warning Light: #FFA940

/* Peligro - Rojo suave */
Danger: #F5222D
Danger Light: #FF4D4F

/* Neutros - Grises suaves */
Background: #F5F7FA
Surface: #FFFFFF
Border: #E8E8E8
Text Primary: #262626
Text Secondary: #8C8C8C
Text Disabled: #BFBFBF
```

### **Colores por Nivel de Riesgo**
```css
/* Para hallazgos de análisis */
Riesgo Alto: #FF4D4F (Rojo suave)
Riesgo Medio: #FA8C16 (Naranja suave)
Riesgo Bajo: #52C41A (Verde suave)
Información: #1890FF (Azul suave)
```

---

## **Tipografía**

### **Fuentes**
- **Principal:** Inter, Roboto, o System Default (Sans-serif)
- **Tamaños:**
  - H1 (Títulos principales): 24-28px, Bold
  - H2 (Subtítulos): 20-22px, SemiBold
  - H3 (Secciones): 18px, Medium
  - Body (Texto normal): 16px, Regular
  - Small (Ayudas, labels): 14px, Regular
  - Caption (Notas): 12px, Regular

### **Jerarquía Visual**
- Títulos: Color primario o texto oscuro
- Texto secundario: Gris medio (#8C8C8C)
- Enlaces: Color primario, subrayado al hover
- Énfasis: Negrita o color primario

---

## **Componentes de Diseño**

### **1. Cards (Tarjetas)**
**Inspiración: Siigo + Odoo**
- Fondo blanco
- Sombra suave: `box-shadow: 0 2px 8px rgba(0,0,0,0.08)`
- Border radius: 8-12px
- Padding: 16-24px
- Hover: Elevación ligera

### **2. Botones**
**Estilos:**
- **Primario:** Fondo color primario, texto blanco, border-radius 8px
- **Secundario:** Borde color primario, fondo transparente
- **Texto:** Solo texto, sin borde ni fondo
- **Tamaños:** Small (32px), Medium (40px), Large (48px)
- **Estados:** Hover (más oscuro), Active (más claro), Disabled (opacidad 0.5)

### **3. Inputs (Campos de Formulario)**
**Inspiración: Odoo**
- Borde suave: 1px solid #E8E8E8
- Border radius: 6px
- Padding: 12px
- Focus: Borde color primario, sombra suave
- Label arriba del campo
- Placeholder en gris claro

### **4. Galería de Fotos**
**Inspiración: Google Fotos**
- Grid responsivo (2-3 columnas en móvil)
- Miniaturas con aspect ratio 1:1
- Border radius: 8px
- Sombra suave en hover
- Overlay al seleccionar (checkmark)
- Transición suave al ampliar

### **5. Listas**
**Inspiración: Odoo**
- Items con padding vertical 12px
- Separador sutil entre items
- Icono a la izquierda (opcional)
- Acciones a la derecha
- Hover: Fondo gris muy claro

### **6. Badges y Tags**
- Fondo suave según categoría
- Texto pequeño, bold
- Border radius: 12px (pill shape)
- Padding: 4px 12px

### **7. Modales y Diálogos**
- Fondo semi-transparente (backdrop)
- Card centrado con sombra
- Border radius: 12px
- Animación de entrada suave (fade + scale)

---

## **Espaciado**

### **Sistema de Espaciado (8px base)**
```
4px   - XS
8px   - Small
16px  - Medium (default)
24px  - Large
32px  - XL
48px  - XXL
```

### **Aplicación:**
- Padding de cards: 16-24px
- Margen entre elementos: 16px
- Espaciado entre secciones: 32px
- Padding de página: 16-24px

---

## **Iconografía**

### **Estilo:**
- **Outline icons** (líneas, no relleno)
- Tamaño estándar: 24px
- Color: Texto secundario o primario
- Espaciado con texto: 8px

### **Biblioteca sugerida:**
- Material Icons (Google)
- Font Awesome (versión outline)
- Custom SVG icons

---

## **Animaciones y Transiciones**

### **Principios:**
- **Suaves:** Duración 200-300ms
- **Naturales:** Easing: ease-in-out o cubic-bezier
- **Funcionales:** Cada animación tiene propósito

### **Transiciones comunes:**
```css
/* Hover */
transition: all 0.2s ease-in-out;

/* Aparecer */
animation: fadeIn 0.3s ease-in-out;

/* Deslizar */
animation: slideIn 0.3s ease-out;
```

---

## **Estados Visuales**

### **Loading (Cargando)**
- Spinner suave (color primario)
- Skeleton screens (placeholders animados)
- Progress bars para procesos largos

### **Éxito**
- Color verde suave
- Icono de check
- Mensaje claro

### **Error**
- Color rojo suave
- Icono de alerta
- Mensaje descriptivo

### **Advertencia**
- Color naranja suave
- Icono de advertencia
- Mensaje informativo

---

## **Navegación**

### **Bottom Navigation (Móvil)**
- 3-5 items principales
- Icono + texto
- Color activo: Primario
- Fondo blanco con sombra superior

### **Sidebar (Desktop/Tablet)**
- Menú lateral colapsable
- Logo arriba
- Items con icono + texto
- Sección activa destacada

---

## **Responsive Design**

### **Breakpoints:**
- **Móvil:** < 600px (1 columna)
- **Tablet:** 600px - 960px (2 columnas)
- **Desktop:** > 960px (3+ columnas)

### **Principios:**
- Mobile-first approach
- Contenido adaptable
- Touch-friendly (mínimo 44x44px para botones)

---

## **Accesibilidad**

### **Contraste:**
- Texto sobre fondo: Mínimo 4.5:1
- Texto grande: Mínimo 3:1

### **Touch Targets:**
- Mínimo 44x44px
- Espaciado entre elementos táctiles

### **Navegación por Teclado:**
- Focus visible
- Orden lógico de tabulación

---

## **Ejemplos de Aplicación**

### **Página de Login (Inspiración: Siigo)**
- Fondo suave (gris muy claro)
- Card centrado con sombra suave
- Logo arriba
- Campos de input con labels
- Botón primario grande
- Enlaces secundarios discretos

### **Galería de Fotos (Inspiración: Google Fotos)**
- Grid de miniaturas
- Tap para ampliar (fullscreen)
- Gestos de zoom y pan
- Barra de acciones inferior
- Checkbox overlay al seleccionar

### **Dashboard (Inspiración: Odoo)**
- Cards con métricas
- Gráficos simples y claros
- Filtros en la parte superior
- Lista de items recientes
- Acciones rápidas visibles

---

## **Herramientas de Diseño**

### **Sugerencias:**
- **Figma:** Para mockups y prototipos
- **Material Design:** Guía de componentes
- **Color Tools:** Coolors.co, Material Palette

---

## **Checklist de Diseño**

Antes de implementar una pantalla:
- [ ] ¿Es suave y agradable visualmente?
- [ ] ¿Es práctica y funcional?
- [ ] ¿Sigue la paleta de colores?
- [ ] ¿Usa el sistema de espaciado?
- [ ] ¿Tiene transiciones suaves?
- [ ] ¿Es responsive?
- [ ] ¿Es accesible?

---

**Esta guía será nuestra referencia para mantener consistencia en todo el diseño de VisioAnalytica.**

