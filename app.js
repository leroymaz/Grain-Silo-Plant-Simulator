const $ = (selector, root=document) => root.querySelector(selector);
const $$ = (selector, root=document) => [...root.querySelectorAll(selector)];
const fmt = n => Number(n).toLocaleString(undefined,{maximumFractionDigits:1});

const state = {
  plantRunning:true,
  emergencyStop:false,
  activeFault:null,
  xp:0,
  credits:250,
  dryerSetpoint:65,
  furnaceSetpoint:180,
  receipts:1050,
  dispatches:0,
  losses:0,
  logs:[],
  missionCompleted:new Set(),

  nodes:[
    {id:'receiving',name:'Receiving Port',icon:'🚛',type:'receiving',stock:250,capacity:500,moisture:18,temperature:24,impurities:.8,quality:87},
    {id:'holding',name:'Holding Bin',icon:'🛢️',type:'holding',stock:120,capacity:500,moisture:14.1,temperature:21,impurities:.5,quality:92},

    ...Array.from({length:7},(_,i)=>({
      id:`silo${i+1}`,
      name:`Silo ${i+1}`,
      icon:'🌾',
      type:'silo',
      stock:[720,540,880,310,630,470,790][i],
      capacity:1000,
      moisture:[13.5,13.8,12.8,14.2,13.2,13.7,13.1][i],
      temperature:[20.2,21.1,18.4,22.1,19.4,20.7,19.8][i],
      impurities:[.4,.5,.3,.6,.4,.5,.4][i],
      quality:[94,92,96,89,94,92,95][i]
    })),

    {id:'dryer',name:'Dryer',icon:'🔥',type:'dryer',stock:0,capacity:100,moisture:18,temperature:65,impurities:.5,quality:88},
    {id:'dispatch',name:'Dispatch Bin',icon:'📦',type:'dispatch',stock:0,capacity:400,moisture:13.2,temperature:20,impurities:.4,quality:94}
  ],

  equipment:[
    {name:'Receiving Conveyor',icon:'↗',status:'Running',speed:85,temp:35},
    {name:'Bucket Elevator 1',icon:'⇧',status:'Running',speed:85,temp:48},
    {name:'Transfer Conveyor A',icon:'⇢',status:'Running',speed:80,temp:34},
    {name:'Transfer Conveyor B',icon:'⇢',status:'Running',speed:80,temp:36},
    {name:'Dryer',icon:'♨',status:'Running',speed:75,temp:65},
    {name:'Furnace',icon:'♨',status:'Running',speed:70,temp:180},
    {name:'Aeration Fans',icon:'❋',status:'Running',speed:90,temp:28},
    {name:'Dispatch Conveyor',icon:'⇢',status:'Running',speed:80,temp:33},
    {name:'Loadout Gate',icon:'▤',status:'Standby',speed:0,temp:25}
  ]
};

const missions = [
 {
  id:'transfer',
  title:'Complete a safe grain transfer',
  description:'Move grain from the Holding Bin to Silo 1 without triggering a fault.',
  reward:100,
  check:()=>state.logs.some(l=>l.type==='TRANSFER'&&l.event.includes('Holding Bin')&&l.event.includes('Silo 1'))
 },
 {
  id:'inspect',
  title:'Inspect plant health',
  description:'Run the plant health check and review the operating recommendations.',
  reward:75,
  check:()=>state.logs.some(l=>l.event==='Plant health check completed')
 },
 {
  id:'quality',
  title:'Review grain quality',
  description:'Open Grain quality and apply the dryer setpoints.',
  reward:75,
  check:()=>state.logs.some(l=>l.event==='Dryer setpoints applied')
 },
 {
  id:'fault',
  title:'Practice fault recovery',
  description:'Inject a training fault, then clear it safely.',
  reward:150,
  check:()=>state.logs.some(l=>l.event==='Training fault cleared')
 },
 {
  id:'inventory',
  title:'Understand inventory',
  description:'Visit the Inventory page and inspect current stock levels.',
  reward:50,
  check:()=>state.logs.some(l=>l.event==='Inventory inspected')
 }
];

const locationById = id => state.nodes.find(n=>n.id===id);

function log(type,event,details='—'){
  state.logs.unshift({
    time:new Date().toLocaleTimeString(),
    type,
    event,
    details
  });

  if(state.logs.length>200) state.logs.length=200;

  renderLogs();
  renderAlarmCount();
}

function toast(message){
  const el=$('#toast');
  el.textContent=message;
  el.classList.add('show');

  clearTimeout(toast.timer);

  toast.timer=setTimeout(
    ()=>el.classList.remove('show'),
    3300
  );
}

function totalStock(){
  return state.nodes.reduce((sum,n)=>sum+n.stock,0);
}

function averageQuality(){
  const grain=state.nodes.filter(n=>n.stock>0);

  return grain.length
    ? Math.round(grain.reduce((s,n)=>s+n.quality,0)/grain.length)
    : 0;
}

function alarmCount(){
  return (state.emergencyStop?1:0)+(state.activeFault?1:0);
}

function healthPercent(){
  return state.equipment.filter(e=>e.status==='Running').length
    /state.equipment.length*100;
}

function navigate(view){
  $$('.view').forEach(v=>
    v.classList.toggle('active',v.id===`view-${view}`)
  );

  $$('.nav-item').forEach(b=>
    b.classList.toggle('active',b.dataset.view===view)
  );

  const titles={
    dashboard:'Plant dashboard',
    plant:'Plant map',
    inventory:'Grain inventory',
    quality:'Grain quality',
    equipment:'Equipment & faults',
    missions:'Learning missions',
    reports:'Event log'
  };

  $('#pageTitle').textContent=titles[view]||'Plant dashboard';
  $('#sidebar').classList.remove('open');

  if(view==='inventory'){
    log('SYSTEM','Inventory inspected','Current stock levels reviewed');
  }

  renderAll();
}

function getLocationOptions(){
  return state.nodes.map(n=>
    `<option value="${n.id}">${n.name}</option>`
  ).join('');
}

function initSelectors(){
  const opts=getLocationOptions();

  $('#sourceSelect').innerHTML=opts;
  $('#destinationSelect').innerHTML=opts;

  $('#sourceSelect').value='holding';
  $('#destinationSelect').value='silo1';
}

function renderNode(n,full=false){
  const fill=Math.min(100,n.stock/n.capacity*100);

  const faulted=state.activeFault&&(
    (state.activeFault==='Dryer overheat'&&n.id==='dryer')||
    (state.activeFault==='Level sensor failure'&&n.type==='silo')||
    (state.activeFault==='Gate mismatch'&&n.id==='dispatch')
  );

  const processing=n.id==='dryer'&&n.stock>0;

  return `
    <div class="${full?'map-node':'flow-node'} ${faulted?'alarm':''} ${processing?'processing':''}">
      <div class="node-icon">${n.icon}</div>

      ${full
        ? `<h3>${n.name}</h3>`
        : `<div class="node-name">${n.name}</div>`
      }

      ${full
        ? `<p>${n.type==='silo'?'Storage silo':n.type.charAt(0).toUpperCase()+n.type.slice(1)} · Capacity ${fmt(n.capacity)} t</p>
           <strong>${fmt(n.stock)} t</strong>
           <small> stored · ${Math.round(fill)}% full</small>`
        : `<div class="node-stock">${fmt(n.stock)} / ${fmt(n.capacity)} t</div>
           <span class="node-state">${faulted?'FAULT':processing?'PROCESSING':fill>90?'NEAR FULL':'AVAILABLE'}</span>`
      }
    </div>
  `;
}

function renderPlant(){
  const keyIds=[
    'receiving',
    'holding',
    'silo1',
    'silo2',
    'silo3',
    'silo4',
    'silo5',
    'silo6',
    'silo7',
    'dryer',
    'dispatch'
  ];

  const nodes=keyIds.map(id=>locationById(id));

  $('#plantFlow').innerHTML=nodes.map(n=>renderNode(n)).join('');
  $('#fullPlantMap').innerHTML=nodes.map(n=>renderNode(n,true)).join('');
}

function renderMetrics(){
  const total=totalStock();
  const quality=averageQuality();
  const health=Math.round(healthPercent());

  $('#totalStock').textContent=`${fmt(total)} t`;

  $('#qualityMetric').textContent=`${quality}/100`;
  $('#qualityBar').style.width=`${quality}%`;

  $('#healthMetric').textContent=`${health}%`;

  $('#healthCaption').textContent=
    state.activeFault
      ? 'fault detected — inspect equipment'
      : state.plantRunning
        ? 'equipment operating normally'
        : 'plant stopped';

  $('#healthBar').style.width=`${health}%`;

  $('#xpMetric').textContent=`${state.xp} XP`;
  $('#creditsMetric').textContent=state.credits;
  $('#xpBar').style.width=`${Math.min(100,state.xp/10)}%`;

  $('#receiptsMetric').textContent=`${fmt(state.receipts)} t`;
  $('#dispatchesMetric').textContent=`${fmt(state.dispatches)} t`;
  $('#lossesMetric').textContent=`${fmt(state.losses)} t`;

  $('#inventoryTotalTag').textContent=`${fmt(total)} tonnes total`;
}

function renderInventory(){
  const rows=state.nodes.map(n=>{
    const fill=Math.min(100,n.stock/n.capacity*100);

    return `
      <tr>
        <td>${n.icon} &nbsp;${n.name}</td>
        <td>${fmt(n.stock)} t</td>
        <td>${fmt(n.capacity)} t</td>
        <td>
          <div class="fill-cell">
            <div class="fill-track">
              <i style="width:${fill}%"></i>
            </div>
            <span>${Math.round(fill)}%</span>
          </div>
        </td>
        <td>${n.moisture.toFixed(1)}%</td>
        <td>${n.temperature.toFixed(1)}°C</td>
      </tr>
    `;
  }).join('');

  $('#inventoryRows').innerHTML=rows;
}

function qualityStatus(n){
  if(n.moisture>17||n.temperature>70||n.impurities>1){
    return ['Needs attention','bad'];
  }

  if(n.moisture>14.5||n.temperature>40||n.quality<90){
    return ['Monitor','warn'];
  }

  return ['Good',''];
}

function renderQuality(){
  const q=averageQuality();

  $('#qualityBig').innerHTML=`${q}<span>/100</span>`;
  $('#qualityBigBar').style.width=`${q}%`;

  $('#qualitySummary').textContent=
    q>=92
      ? 'Grain quality is within the training target.'
      : q>=85
        ? 'Some grain locations need monitoring. Review moisture and temperature.'
        : 'Quality is below the target. Inspect the grain and drying process.';

  $('#dryerTempValue').textContent=state.dryerSetpoint;
  $('#furnaceTempValue').textContent=state.furnaceSetpoint;

  $('#dryerTempRange').value=state.dryerSetpoint;
  $('#furnaceTempRange').value=state.furnaceSetpoint;

  $('#qualityRows').innerHTML=state.nodes
    .filter(n=>n.stock>0)
    .map(n=>{
      const [status,cls]=qualityStatus(n);

      return `
        <tr>
          <td>${n.name}</td>
          <td>${n.moisture.toFixed(1)}%</td>
          <td>${n.temperature.toFixed(1)}°C</td>
          <td>${n.impurities.toFixed(1)}%</td>
          <td><strong>${n.quality}/100</strong></td>
          <td><span class="status-pill ${cls}">${status}</span></td>
        </tr>
      `;
    }).join('');
}

function renderEquipment(){
  const icons=['↗','⇧','⇢','⇢','♨','♨','❋','⇢','▤'];

  $('#equipmentGrid').innerHTML=state.equipment.map((e,i)=>{
    const stopped=e.status==='Stopped';

    const warning=state.activeFault&&(
      (state.activeFault==='Conveyor blockage'&&e.name.includes('Conveyor'))||
      (state.activeFault==='Belt slip'&&e.name.includes('Conveyor'))||
      (state.activeFault==='Elevator bearing warning'&&e.name.includes('Elevator'))||
      (state.activeFault==='Dryer overheat'&&['Dryer','Furnace'].includes(e.name))||
      (state.activeFault==='Gate mismatch'&&e.name==='Loadout Gate')||
      (state.activeFault==='Level sensor failure'&&i===6)
    );

    return `
      <article class="equipment-card">
        <div class="equipment-head">
          <div class="equipment-icon">${e.icon||icons[i]}</div>

          <div>
            <h3>${e.name}</h3>
            <small>${warning?'Training fault detected':'Plant equipment'}</small>
          </div>

          <span class="status-pill ${warning||stopped?'bad':e.status==='Standby'?'warn':''}">
            ${warning?'Fault':e.status}
          </span>
        </div>

        <div class="equipment-stats">
          <span>Speed <strong>${e.speed}%</strong></span>
          <span>Temp <strong>${e.temp}°C</strong></span>
        </div>
      </article>
    `;
  }).join('');
}
