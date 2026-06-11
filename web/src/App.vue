<script setup>
import LiveStream from "./components/LiveStream.vue";

const converterBase =
  "https://gui-cam-converter.yellowdune-f4db7c23.ukwest.azurecontainerapps.io";

const cameras = [
  {
    label: "Camera 1",
    src: import.meta.env.VITE_STREAM_URL || `${converterBase}/cam/index.m3u8`,
    ptz: true,
  },
  {
    label: "Camera 2",
    src:
      import.meta.env.VITE_STREAM_URL_2 ||
      `${converterBase}/cam2/index.m3u8`,
    ptz: false,
  },
];
</script>

<template>
  <div class="page">
    <header class="header">
      <p class="eyebrow">Live from the vivarium</p>
      <h1>GUI Cam</h1>
      <p class="tagline">
        Say hello to <strong>GUI</strong>, a crested gecko doing gecko things in
        real time.
      </p>
    </header>

    <main class="main">
      <div class="cameras">
        <section
          v-for="camera in cameras"
          :key="camera.label"
          class="camera"
        >
          <h2 class="camera-label">{{ camera.label }}</h2>
          <LiveStream :src="camera.src" :ptz-enabled="camera.ptz" />
        </section>
      </div>
    </main>

    <footer class="footer">
      <p>
        Crested geckos are nocturnal tree dwellers from New Caledonia. GUI may
        be napping, climbing, or hunting crickets when you tune in.
      </p>
    </footer>
  </div>
</template>

<style scoped>
.page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 2.5rem 1.25rem 2rem;
  background:
    radial-gradient(
      ellipse 80% 50% at 50% -10%,
      rgba(74, 124, 89, 0.35),
      transparent
    ),
    radial-gradient(circle at 100% 100%, rgba(45, 74, 52, 0.25), transparent),
    #0f1410;
}

.header {
  text-align: center;
  max-width: 40rem;
  margin-bottom: 2rem;
}

.eyebrow {
  margin: 0 0 0.5rem;
  font-size: 0.85rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: #7cb88a;
}

h1 {
  margin: 0 0 0.75rem;
  font-size: clamp(2.25rem, 6vw, 3.25rem);
  font-weight: 700;
  letter-spacing: -0.02em;
  color: #f2f7f0;
}

.tagline {
  margin: 0;
  font-size: 1.05rem;
  color: #a8b8a4;
}

.tagline strong {
  color: #c8e6c0;
  font-weight: 600;
}

.main {
  width: min(960px, 100%);
  flex: 1;
}

.cameras {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.camera {
  min-width: 0;
}

.camera-label {
  margin: 0 0 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: #7cb88a;
}

.footer {
  margin-top: 2rem;
  max-width: 36rem;
  text-align: center;
  font-size: 0.9rem;
  color: #6d7d69;
}

.footer p {
  margin: 0;
}
</style>
